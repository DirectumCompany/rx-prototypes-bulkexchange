using System;
using System.Collections.Generic;
using System.Linq;
using Sungero.BulkExchangeSolution.ExchangeDocumentInfo;
using Sungero.Core;
using Sungero.CoreEntities;
using Sungero.Docflow;
using Sungero.ExchangeCore;
using Sungero.RecordManagement;
using MessageType = Sungero.Core.MessageType;
using Status = Sungero.Workflow.Assignment.Status;
using System.IO;

namespace Sungero.BulkExchangeSolution.Client
{
  public class ModuleFunctions
  {
    public virtual void ImportDocumentsFromFolder(string path)
    {
      var allDirectories = Directory.GetDirectories(path, "*.*", System.IO.SearchOption.AllDirectories);
      var chiefAccountant = Functions.Module.Remote.GetImportedDocumentsResponsible();
      var caseFile = Functions.Module.Remote.GetImportedDocumentsDefaultCaseFile();
      foreach (var directory in allDirectories)
      {
        try
        {
          Logger.DebugFormat("Start import documents from folder {0}.", directory);
          var filesPaths = Directory.GetFiles(directory);
          var files = new List<byte[]>();
          var purchaseNumbers = new List<string>();
          
          foreach (var filesPath in filesPaths)
            files.Add(File.ReadAllBytes(filesPath));
          
          // Извлечение номера заказа из xml файлов.
          if (files.Count == 1 || files.Count == 2)
          {
            foreach (var file in files)
            {
              var byteArray = Docflow.Structures.Module.ByteArray.Create(file);
              var purchaseOrderNumber = Functions.Module.Remote.GetPurchaseNumber(byteArray);
              if (!string.IsNullOrEmpty(purchaseOrderNumber))
                purchaseNumbers.Add(purchaseOrderNumber);
            }
          }
          
          if (purchaseNumbers.Any(n => string.IsNullOrEmpty(n)) || purchaseNumbers.Distinct().Count() > 1)
            continue;
          
          // Импорт и дозаполнение документов.
          var documents = new List<IOfficialDocument>();
          foreach (var filesPath in filesPaths)
          {
            Logger.DebugFormat("Import document from path {0}.", filesPath);
            var document = FinancialArchive.PublicFunctions.Module.ImportFormalizedDocument(filesPath, false);
            document.Save();
            Logger.DebugFormat("Document imported with Id {0}.", document.Id);
            Logger.DebugFormat("Process document with Id {0}.", document.Id);
            Functions.Module.Remote.ProcessImportedDocument(document, chiefAccountant, caseFile);
            documents.Add(document);
          }
          if (documents.Count == 2)
            BulkExchangeSolution.Module.Exchange.PublicFunctions.Module.Remote.AddRelationsForDocuments(documents);
          
          Logger.DebugFormat("Completed import documents from folder {0}.", directory);
        }
        catch (Exception ex)
        {
          Logger.Error(Sungero.BulkExchangeSolution.Resources.CannotImportDocument, ex);
        }
      }
    }
    
    public virtual void SignImportedDocuments()
    {
      var documents = Functions.Module.Remote.GetImportedDocuments().Where(d => d.LastVersionApproved != true);
      foreach (var document in documents)
      {
        try
        {
          var addenda = document.Relations.GetRelated().Select(d => OfficialDocuments.As(d)).ToList();
          var certificate = document.BusinessUnitBox.ExchangeServiceCertificates.Where(x => Equals(x.Certificate.Owner, Users.Current) &&
                                                                                       x.Certificate.Enabled == true).Select(x => x.Certificate).FirstOrDefault();
          
          Logger.DebugFormat("Start sign document with Id {0}.", document.Id);
          if (Docflow.PublicFunctions.OfficialDocument.ApproveWithAddenda(document, addenda, certificate, string.Empty, null, false, null))
            Logger.DebugFormat("Document with Id {0} signed.", document.Id);
          else
            Logger.DebugFormat("Document with Id {0} not signed.", document.Id);
        }
        catch (Exception ex)
        {
          Logger.Error(Sungero.BulkExchangeSolution.Resources.CannotSignDocument, ex);
        }
      }
    }
    
    public virtual void SendDocumentsToCounterparties()
    {
      var documents = Functions.Module.Remote.GetImportedDocuments().Where(d => d.LastVersionApproved == true);
      foreach (var document in documents)
      {
        try
        {
          var addenda = document.Relations.GetRelated().Select(d => OfficialDocuments.As(d)).ToList();
          var certificate = document.BusinessUnitBox.ExchangeServiceCertificates.Where(x => Equals(x.Certificate.Owner, Users.Current) &&
                                                                                       x.Certificate.Enabled == true).Select(x => x.Certificate).FirstOrDefault();
          
          Logger.DebugFormat("Send to counterparty document with Id {0}.", document.Id);
          Exchange.PublicFunctions.Module.Remote.SendDocuments(document.BusinessUnitBox, document, addenda, true,
                                                               string.Empty, document.Counterparty, certificate);
          Logger.DebugFormat("Document with Id {0} sent to counterparty.", document.Id);
          
          var allDocuments = new List<IOfficialDocument>() { document };
          allDocuments.AddRange(addenda);
          foreach (var sendedDocument in allDocuments)
          {
            Logger.DebugFormat("Update exchange document info for document with Id {0}.", sendedDocument.Id);
            var info = BulkExchangeSolution.ExchangeDocumentInfos.As(Exchange.PublicFunctions.ExchangeDocumentInfo.Remote.GetLastDocumentInfo(sendedDocument));
            info.PurchaseOrder = "1";
            info.Save();
          }
        }
        catch (Exception ex)
        {
          Logger.Error(Sungero.BulkExchangeSolution.Resources.CannotSendDocument, ex);
        }
      }
    }
    
    public virtual void SignVerifiedDocuments()
    {
      var verifiedSets = Sungero.BulkExchangeSolution.Functions.Module.Remote.GetVerifiedSets();
      foreach (var verifiedSet in verifiedSets)
      {
        var infos = verifiedSet.ExchangeDocumentInfos.Where(c => c.RootBox.HasExchangeServiceCertificates == true &&
                                                            c.RootBox.ExchangeServiceCertificates.Any(x => Equals(x.Certificate.Owner, Users.Current) && x.Certificate.Enabled == true));
        Logger.DebugFormat("Start sign document set with document ids {0}.", string.Join(", ", infos.Select(i => i.Document.Id)));
        foreach (var info in infos)
        {
          if (info.NeedSign == true)
          {
            var certificate = info.RootBox.ExchangeServiceCertificates.Where(x => Equals(x.Certificate.Owner, Users.Current) && x.Certificate.Enabled == true).Select(x => x.Certificate).FirstOrDefault();
            try
            {
              if (Docflow.PublicFunctions.OfficialDocument.ApproveWithAddenda(info.Document, null, certificate, string.Empty, null, false, null))
              {
                Logger.DebugFormat("Sign document set with document ids {0} successfully.", string.Join(", ", info.Document.Id));
                info.SignStatus = Sungero.BulkExchangeSolution.ExchangeDocumentInfo.SignStatus.Signed;
                info.Save();
              }
            }
            catch (Exception ex)
            {
              Logger.Error(Sungero.BulkExchangeSolution.Resources.CannotSignDocument, ex);
            }
          }
        }
      }
    }
    
    public virtual void RejectDocument(IAccountingDocumentBase document)
    {
      var approvalSigningAssignments = Functions.Module.Remote.GetApprovalSigningAssignments(document);
      if (approvalSigningAssignments.Count() > 1)
      {
        Dialogs.ShowMessage(Sungero.BulkExchangeSolution.Resources.FewAssignmentError, MessageType.Error);
        return;
      }

      var assignment = approvalSigningAssignments.FirstOrDefault();
      if (assignment == null)
      {
        Dialogs.ShowMessage(Sungero.BulkExchangeSolution.Resources.NoAssignmentError, MessageType.Error);
      }
      
      var task = ApprovalTasks.As(assignment.MainTask);
      var dialog = Dialogs.CreateInputDialog(Sungero.BulkExchangeSolution.Resources.Reject);
      var abortingReason = dialog.AddMultilineString(Sungero.BulkExchangeSolution.Resources.RejectReason, true);
      dialog.Buttons.AddOkCancel();
      dialog.Buttons.Default = DialogButtons.Ok;
      
      dialog.SetOnButtonClick(args =>
                              {
                                if (!string.IsNullOrEmpty(abortingReason.Value) && string.IsNullOrWhiteSpace(abortingReason.Value))
                                  args.AddError(Resources.EmptyAbortingReason, abortingReason);
                              });
      
      if (dialog.Show() == DialogButtons.Ok)
      {
        assignment.ActiveText += string.IsNullOrWhiteSpace(assignment.ActiveText)
          ? abortingReason.Value
          : Environment.NewLine + abortingReason.Value;

        try
        {
          // Подписание согласующей подписью с результатом "не согласовано".
          var isSigned = Signatures.NotEndorse(document.LastVersion, null, abortingReason.Value, assignment.Performer);
          foreach (var attachment in task.AddendaGroup.OfficialDocuments)
          {
            isSigned &= Signatures.NotEndorse(attachment.LastVersion, null, abortingReason.Value, assignment.Performer);
          }

          if (isSigned)
            assignment.Complete(Sungero.Docflow.ApprovalSigningAssignment.Result.Abort);
        }
        catch (CommonLibrary.Exceptions.PlatformException ex)
        {
          if (!ex.IsInternal)
          {
            var message = ex.Message.EndsWith(".") ? ex.Message : string.Format("{0}.", ex.Message);
            Dialogs.ShowMessage(message, MessageType.Error);
          }
          else
            throw;
        }
      }
    }
    
    public virtual void SendRejectedDocuments()
    {
      var needReject = Functions.Module.Remote.GetRejectedDocumentInfos();
      foreach (var documentInfo in needReject)
      {
        var certificate = documentInfo.RootBox.ExchangeServiceCertificates
          .Where(x => Equals(x.Certificate.Owner, Users.Current) && x.Certificate.Enabled == true)
          .Select(x => x.Certificate)
          .FirstOrDefault();
        try
        {
          {
            var result = Sungero.Exchange.PublicFunctions.Module.SendAmendmentRequest(new List<IOfficialDocument> { documentInfo.Document },
                                                                                      documentInfo.Counterparty, Sungero.BulkExchangeSolution.Resources.RejectMessage, true,
                                                                                      documentInfo.RootBox, certificate, false);
            if (result == string.Empty)
            {
              documentInfo.RejectionStatus = RejectionStatus.Sent;
              Logger.DebugFormat("Send reject document with document ids {0} successfully.", documentInfo.Document.Id);
            }
            else
              Logger.Debug(result);

            if (string.Equals(result, Sungero.Exchange.Resources.AllAnswersIsAlreadySent) ||
                string.Equals(result, Sungero.Exchange.Resources.AnswerIsAlreadySent))
              documentInfo.RejectionStatus = RejectionStatus.Sent;
            documentInfo.Save();
          }
        }
        catch (Exception ex)
        {
          Logger.Error(Sungero.BulkExchangeSolution.Resources.CannotSignDocument, ex);
        }
      }
    }
    
    public void ShowApprovalSigningAssignments(IAccountingDocumentBase document)
    {
      var assignments = Functions.Module.Remote.GetApprovalSigningAssignments(document);
      if (assignments.Count() == 1)
        assignments.FirstOrDefault().ShowModal();
      else
        assignments.ShowModal();
    }
    
    public virtual void SignDocumentSets(System.Collections.Generic.IEnumerable<IAccountingDocumentBase> documents, Sungero.Domain.Client.ExecuteActionArgs e)
    {
      if (documents.Select(d => d.BusinessUnitBox).Distinct().Count() > 1)
      {
        ShowResultDialog(new List<string>() { Sungero.BulkExchangeSolution.Resources.MultipleBusinessUnitsError });
        return;
      }
      
      var dialog = Dialogs.CreateTaskDialog(Resources.SignDocumentSetsConfirmationMessage, MessageType.Question);
      dialog.Buttons.AddYesNo();
      dialog.Buttons.Default = DialogButtons.Yes;
      if (dialog.Show() != DialogButtons.Yes)
        return;
      
      var businessUnitBox = documents.Select(d => d.BusinessUnitBox).FirstOrDefault();
      var currentEmployee = Company.Employees.Current;
      var certificate = Sungero.Exchange.PublicFunctions.Module.GetUserExchangeCertificate(businessUnitBox, currentEmployee);
      
      if (certificate == null)
      {
        ShowResultDialog(new List<string>() { Exchange.Resources.CertificateNotFound });
        return;
      }
      
      var errortList = new List<string>();
      var exceptionDictionary = new Dictionary<string, int>();
      var documentsInMultipleAssignments = 0;
      var notSignedDocuments = 0;
      foreach (var document in documents)
      {
        var approvalSigningAssignments = Functions.Module.Remote.GetApprovalSigningAssignments(document);
        
        if (!approvalSigningAssignments.Any())
          continue;
        
        if (approvalSigningAssignments.Count() > 1)
        {
          documentsInMultipleAssignments++;
          continue;
        }
        
        var approvalSigningAssignment = approvalSigningAssignments.Single();
        
        var addendaGroupDocuments = approvalSigningAssignment.AddendaGroup.OfficialDocuments;
        var addendas = new List<IOfficialDocument>();
        foreach (var item in addendaGroupDocuments)
        {
          var info = Functions.ExchangeDocumentInfo.Remote.GetExchangeDocumentInfo(item);
          if (info == null ? !IncomingTaxInvoices.Is(item) : info.SignStatus == ExchangeDocumentInfo.SignStatus.Required)
            addendas.Add(item);
        }
        
        var activeText = string.IsNullOrWhiteSpace(approvalSigningAssignment.ActiveText) ? string.Empty : approvalSigningAssignment.ActiveText;
        
        try
        {
          if (!Docflow.PublicFunctions.OfficialDocument.ApproveWithAddenda(document, addendas, certificate, activeText, null, false, null))
            notSignedDocuments++;
          else
            approvalSigningAssignment.Complete(Docflow.ApprovalSigningAssignment.Result.Sign);
        }
        catch (CommonLibrary.Exceptions.PlatformException ex)
        {
          if (!ex.IsInternal)
          {
            var message = ex.Message.ToLower().TrimEnd('.');
            if (exceptionDictionary.ContainsKey(message))
              exceptionDictionary[message]++;
            else
              exceptionDictionary.Add(message, 1);
          }
          else
          {
            throw;
          }
        }
      }
      
      if (exceptionDictionary.Any())
        foreach (var exception in exceptionDictionary)
          errortList.Add(string.Format("  - {0} - {1}", exception.Key, exception.Value));
      
      if (notSignedDocuments > 0)
        errortList.Insert(0, Sungero.BulkExchangeSolution.Resources.CannotSignDocumentsFormat(notSignedDocuments));
      
      if (documentsInMultipleAssignments > 0)
        errortList.Insert(0, string.Format("  - {0} - {1}", Sungero.BulkExchangeSolution.Resources.FewAssignmentError.ToString().ToLower(), documentsInMultipleAssignments));
      
      if (errortList.Any())
      {
        errortList.Insert(0, Resources.SomeDocumentsNotSigned);
        ShowResultDialog(errortList);
      }
    }
    
    private static void ShowResultDialog(List<string> textList)
    {
      var text = string.Join(Environment.NewLine, textList);
      Dialogs.ShowMessage(Resources.DoumentsSignError, text, MessageType.Error);
    }
    
    public void ShowDocumentSet(IAccountingDocumentBase document)
    {
      var assignments = Functions.Module.Remote.GetApprovalSigningAssignments(document).ToList();
      if (assignments.Count() > 1)
      {
        Dialogs.ShowMessage(Sungero.BulkExchangeSolution.Resources.FewAssignmentError, MessageType.Error);
        return;
      }
      
      if (assignments.Count() == 1)
      {
        var documents = assignments.SelectMany(x => x.AllAttachments).Select(a => OfficialDocuments.As(a)).ToList();
        documents.ShowModal();
      }
      else
        Dialogs.ShowMessage(Resources.NoAssignmentError, MessageType.Error);
    }
  }
}