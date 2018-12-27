using System;
using System.Collections.Generic;
using System.Linq;
using Sungero.BulkExchangeSolution.ExchangeDocumentInfo;
using Sungero.Core;
using Sungero.CoreEntities;
using Sungero.Docflow;
using Sungero.Docflow.AccountingDocumentBase;
using Sungero.FinancialArchive;
using Sungero.Workflow;
using Sungero.Workflow.Server;
using Sungero.Workflow.SimpleTask;

namespace Sungero.BulkExchangeSolution.Module.Exchange.Server
{
  partial class ModuleJobs
  {

    /// <summary>
    /// Отправить подписанные документы в сервис обмена.
    /// </summary>
    public virtual void SendSignedDocuments()
    {
      var infos = Functions.Module.GetSignedAndNotSendedDocuments();
      var boxes = infos.GroupBy(i => i.RootBox);
      foreach (var box in boxes)
      {
        var counterparties = box.GroupBy(i => i.Counterparty);
        foreach (var counterpartyGroup in counterparties)
        {
          try
          {
            var documents = counterpartyGroup.Select(g => g.Document).ToList();
            Sungero.Exchange.PublicFunctions.Module.Remote.SendAnswers(documents, box.Key, counterpartyGroup.Key, BusinessUnitBoxes.As(box.Key).SignDocumentCertificate);
          }
          catch (AppliedCodeException ex)
          {
            Logger.Debug(ex.ToString());
          }
        }
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
        var document = AccountingDocumentBases.As(documentsInfo.Document);
        if (UniversalTransferDocuments.Is(document) ||
            Waybills.Is(document) && document.Relations.GetRelated().FirstOrDefault(IncomingTaxInvoices.Is) != null)
          documentsInfo.CheckStatus = document.TotalAmount < 100000 ? CheckStatus.Completed : CheckStatus.Required;
        
        if (document.IsAdjustment == true || Equals(document.Note.ToLowerInvariant().Trim(), "проведено"))
          documentsInfo.CheckStatus = CheckStatus.Completed;

        if (documentsInfo.CheckStatus == CheckStatus.Required && Calendar.Now - document.Created > TimeSpan.FromHours(1))
        {
          var subject = "Не пройдена проверка:";
          if (documentsInfo.CheckTask != null && documentsInfo.CheckTask.Status == Workflow.Task.Status.InProcess)
            continue;
          if (documentsInfo.CheckTask != null && documentsInfo.CheckTask.Status == Workflow.Task.Status.Completed &&
              this.IsCheckDocumentCompleted(document))
            documentsInfo.CheckStatus = CheckStatus.Completed;
          else
          {
            var task = SimpleTasks.Create(subject + " " + document.Name, Calendar.Today.AddWorkingDays(1),
                                          ExchangeCore.PublicFunctions.BoxBase.GetExchangeDocumentResponsible(documentsInfo.RootBox,
                                                                                                              documentsInfo.Counterparty));
            task.Attachments.Add(documentsInfo.Document);
            task.Start();
            documentsInfo.CheckTask = task;
          }
        }
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