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
        Transactions.Execute(
          () =>
          {
            Logger.DebugFormat("Process document set with infos {0}", string.Join(", ", documentSet.ExchangeDocumentInfos.Select(i => i.Id)));
            var boxes = documentSet.ExchangeDocumentInfos.GroupBy(i => i.RootBox);
            foreach (var box in boxes)
            {
              var counterparties = box.GroupBy(i => i.Counterparty);
              foreach (var counterpartyGroup in counterparties)
              {
                var businessUnitBox = BusinessUnitBoxes.As(box.Key);
                var counterparty = counterpartyGroup.Key;
                var documents = counterpartyGroup.Select(g => g.Document).ToList();
                Logger.DebugFormat("Try to send answer to documents {0}. Box {1}, counterparty {2}, certificate {3}.",
                                   string.Join(", ", documents.Select(d => d.Id)), businessUnitBox.Id, counterparty.Id, businessUnitBox.SignDocumentCertificate.Id);
                Sungero.Exchange.PublicFunctions.Module.Remote.SendAnswers(documents, businessUnitBox, counterparty, businessUnitBox.SignDocumentCertificate);
              }
            }
          });
      }
    }
    
    /// <summary>
    /// 
    /// </summary>
    public virtual void CheckDocuments()
    {
      var documentsInfos =
        ExchangeDocumentInfos.GetAll(d => (d.CheckStatus == CheckStatus.Required) && d.PurchaseOrder != null);
      foreach (var documentsInfo in documentsInfos)
      {
        var result = true;
        var reason = string.Empty;
        var document = AccountingDocumentBases.As(documentsInfo.Document);

        if (UniversalTransferDocuments.Is(document) && (document.FormalizedFunction == FormalizedFunction.SchfDop) ||
            document.FormalizedFunction == FormalizedFunction.Dop &&
            document.Relations.GetRelated().FirstOrDefault(IncomingTaxInvoices.Is) != null)
        {
          if (document.Currency != Currencies.GetAll().FirstOrDefault(x => x.AlphaCode == Sungero.BulkExchangeSolution.Module.Exchange.Resources.RubAlphaCode))
          {
            result = false;
            reason = Sungero.BulkExchangeSolution.Module.Exchange.Resources.CurrencyError;
          }

          if (document.TotalAmount >= 100000)
          {
            result = false;
            reason = Sungero.BulkExchangeSolution.Module.Exchange.Resources.SummaryIsTooBig;
          }

          if (document.Relations.GetRelated().FirstOrDefault(IncomingTaxInvoices.Is) != null && document.TotalAmount != AccountingDocumentBases
              .As(document.Relations.GetRelated().FirstOrDefault(IncomingTaxInvoices.Is)).TotalAmount)
          {
            result = false;
            reason = Sungero.BulkExchangeSolution.Module.Exchange.Resources.DocumentsSummaryError;
          }
        }
        else
        {
          result = false;
          reason = Sungero.BulkExchangeSolution.Module.Exchange.Resources.DocumentSetError;
        }

        if (!result)
        {
          var subject = string.Format(Sungero.BulkExchangeSolution.Module.Exchange.Resources.CheckFailed + " exchangeDocumentInfoId={0} ", documentsInfo.Id);
          Logger.Error(subject + reason);
          if (Calendar.Now - document.Created > TimeSpan.FromHours(1))
          {
            if (documentsInfo.CheckTask != null && documentsInfo.CheckTask.Status == Workflow.Task.Status.InProcess)
              continue;
            if (documentsInfo.CheckTask != null && documentsInfo.CheckTask.Status == Workflow.Task.Status.Completed &&
                this.IsCheckDocumentCompleted(document))
              documentsInfo.CheckStatus = CheckStatus.Completed;
            else
            {
              var task = Sungero.Exchange.PublicFunctions.Module.Remote.CreateExchangeTask(documentsInfo.RootBox,
                                                                                           documentsInfo.Counterparty,
                                                                                           documentsInfo.MessageDate.Value, true);
              task.NeedSigning.All.Add(documentsInfo.Document);
              task.ActiveText = Resources.CheckFailed;
              task.ActiveText += reason;

              task.Start();
              documentsInfo.CheckTask = task;
            }
          }
        }

        if (result)
        {
          documentsInfo.CheckStatus = CheckStatus.Completed;
          Logger.Debug(Sungero.BulkExchangeSolution.Module.Exchange.Resources.CheckReturnRevocationResultFormat(documentsInfo.Id));
        }
        else
          documentsInfo.CheckStatus = CheckStatus.Required;

        documentsInfo.Save();
      }
    }

    private bool IsCheckDocumentCompleted(IOfficialDocument document)
    {
      return document.ExchangeState == Docflow.OfficialDocument.ExchangeState.Signed || document.ExchangeState == Docflow.OfficialDocument.ExchangeState.Obsolete ||
        document.ExchangeState == Docflow.OfficialDocument.ExchangeState.Rejected || document.ExchangeState == Docflow.OfficialDocument.ExchangeState.Terminated;
    }
  }
}