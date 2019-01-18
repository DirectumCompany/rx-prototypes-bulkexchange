using System;
using System.Collections.Generic;
using System.Linq;
using Sungero.BulkExchangeSolution.ExchangeDocumentInfo;
using Sungero.Commons;
using Sungero.Core;
using Sungero.Docflow;
using Sungero.Exchange;
using Sungero.FinancialArchive;
using Sungero.FinancialArchive.UniversalTransferDocument;
using FormalizedFunction = Sungero.Docflow.AccountingDocumentBase.FormalizedFunction;

namespace Sungero.BulkExchangeSolution.Module.Exchange.Server
{
  partial class ModuleJobs
  {

    /// <summary>
    /// Отправить подписанные документы в сервис обмена.
    /// </summary>
    public virtual void SendSignedDocuments()
    {
      var documentSets = Functions.Module.GetSignedAndNotSendedDocumentSets();
      foreach (var documentSet in documentSets)
      {
        var infoIds = documentSet.ExchangeDocumentInfos.Select(e => e.Id).ToList();
        Transactions.Execute(
          () =>
          {
            Logger.DebugFormat("Process document set with infos {0}", string.Join(", ", infoIds));
            var infos = ExchangeDocumentInfos
              .GetAll(i => i.ExchangeState == ExchangeDocumentInfo.ExchangeState.SignRequired && infoIds.Contains(i.Id)).ToList();
            var boxes = infos.GroupBy(i => i.RootBox);
            foreach (var box in boxes)
            {
              var counterparties = box.GroupBy(i => i.Counterparty);
              foreach (var counterpartyGroup in counterparties)
              {
                var businessUnitBox = ExchangeCore.BusinessUnitBoxes.As(box.Key);
                var counterparty = counterpartyGroup.Key;
                var documents = counterpartyGroup.Select(g => g.Document).ToList();
                var document = documents.First();
                
                var certificates = Functions.Module.GetDocumentCertificatesToBox(document, businessUnitBox);
                var certificate = certificates.Certificates.FirstOrDefault();

                Logger.DebugFormat("Try to send answer to documents {0}. Box {1}, counterparty {2}, certificate {3}.",
                                   string.Join(", ", documents.Select(d => d.Id)), businessUnitBox.Id, counterparty.Id, certificate.Id);
                Sungero.Exchange.PublicFunctions.Module.Remote.SendAnswers(documents, businessUnitBox, counterparty, certificate, true);
              }
            }
          });
      }
    }
    
    /// <summary>
    /// Сверка документов.
    /// </summary>
    public virtual void VerifyDocuments()
    {
      var infos = ExchangeDocumentInfos.GetAll(d => (d.VerificationStatus == VerificationStatus.Required) && d.PurchaseOrder != null);
      var documentSets = BulkExchangeSolution.Functions.ExchangeDocumentInfo.GetDocumentSets(infos.ToList()).Where(s => s.IsFullSet).ToList();
      foreach (var documentSet in documentSets)
      {
        Functions.Module.VerifyDocumentSet(documentSet);
      }
    }
  }
}