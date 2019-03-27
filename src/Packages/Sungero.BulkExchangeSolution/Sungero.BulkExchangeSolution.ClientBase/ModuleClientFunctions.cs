using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Sungero.BulkExchangeSolution.ExchangeDocumentInfo;
using Sungero.Core;
using Sungero.CoreEntities;
using Sungero.Docflow;
using Sungero.ExchangeCore;
using Sungero.FinancialArchive;
using Sungero.RecordManagement;
using MessageType = Sungero.Core.MessageType;
using Status = Sungero.Workflow.Assignment.Status;

namespace Sungero.BulkExchangeSolution.Client
{
  public class ModuleFunctions
  {
    #region Загрузка документов из папки
    
    public virtual void ImportDocumentsFromFolder(string path)
    {
      var allDirectories = Directory.GetDirectories(path, "*.*", System.IO.SearchOption.AllDirectories);
      var chiefAccountant = Functions.Module.Remote.GetImportedWaybillDocumentsResponsible();
      foreach (var directory in allDirectories)
      {
        try
        {
          Logger.DebugFormat("Start import documents from folder {0}.", directory);
          var filesPaths = Directory.GetFiles(directory);
          var purchaseNumbers = new List<string>();
          var contractNumbers = new List<string>();

          if (filesPaths.Count() == 1 || filesPaths.Count() == 2)
            foreach (var filesPath in filesPaths)
              this.CollectPurchaseAndContractNumbers(filesPath, purchaseNumbers, contractNumbers);
          
          var isValid = this.ValidatePurchaseAndContractNumbers(purchaseNumbers, contractNumbers);
          if (!isValid)
            continue;
          
          // Импорт документов.
          var documents = new List<IOfficialDocument>();
          foreach (var filesPath in filesPaths)
          {
            var document = this.ImportDocument(filesPath);
            if (document != null)
              documents.Add(document);
          }
          
          if (documents.Count == 2)
            BulkExchangeSolution.Module.Exchange.PublicFunctions.Module.Remote.AddRelationsForDocuments(documents);
          
          Logger.DebugFormat("Completed import documents from folder {0}.", directory);
          
          var isContractStatementDocumentSet = contractNumbers.Any();
          var chief = this.GetChiefBusinessUnit(documents);
          var businesUnit = this.GetBusinessUnit(documents);
          var department = Functions.Module.Remote.GetDepartmentByName(businesUnit, "Отдел продаж");
          var salesManager = Functions.Module.Remote.GetEmployeeByJobTitle(department, "Менеджер по продажам");
          var responsible = isContractStatementDocumentSet ? salesManager : chiefAccountant;
          var titleSignatory = isContractStatementDocumentSet ? chief : chiefAccountant;
          
          this.ProcessImportedDocuments(documents, responsible, titleSignatory, isContractStatementDocumentSet);
          
          if (isContractStatementDocumentSet)
          {
            this.ProcessContractStatementDocuments(contractNumbers, documents, chief);

            if (this.CreateAndStartApprovalTask(documents, salesManager, chief))
            {
              var schf = Docflow.AccountingDocumentBase.FormalizedFunction.Schf;
              var contractStatement = documents.FirstOrDefault(d => AccountingDocumentBases.As(d).FormalizedFunction != schf);
              Logger.DebugFormat("Completed start approval task from document with Id {0}.", contractStatement.Id);
            }
          }
          else
          {
            this.ProcessWaybillDocuments(documents);
          }
        }
        catch (Exception ex)
        {
          Logger.Error(Sungero.BulkExchangeSolution.Resources.CannotImportDocument, ex);
        }
      }
    }
    
    public virtual void SignImportedDocuments()
    {
      var documents = Functions.Module.Remote.GetImportedDocuments().Where(d => d.LastVersionApproved != true && !d.Note.Contains("Номер договора"));
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
      var chiefAccountant = Functions.Module.Remote.GetImportedWaybillDocumentsResponsible();
      foreach (var document in documents)
      {
        try
        {
          var addenda = document.Relations.GetRelated().Select(d => OfficialDocuments.As(d)).ToList();
          var certificate = document.BusinessUnitBox.ExchangeServiceCertificates.Where(x => Equals(x.Certificate.Owner, chiefAccountant) &&
                                                                                       x.Certificate.Enabled == true).Select(x => x.Certificate).FirstOrDefault();
          
          Logger.DebugFormat("Send to counterparty document with Id {0}.", document.Id);
          Exchange.PublicFunctions.Module.Remote.SendDocuments(document, addenda,  document.Counterparty, document.BusinessUnitBox,
                                                               certificate, true, string.Empty);
          Logger.DebugFormat("Document with Id {0} sent to counterparty.", document.Id);
          
          var allDocuments = new List<IOfficialDocument>() { document };
          allDocuments.AddRange(addenda);
          foreach (var sendedDocument in allDocuments)
          {
            Logger.DebugFormat("Update exchange document info for document with Id {0}.", sendedDocument.Id);
            var info = BulkExchangeSolution.ExchangeDocumentInfos.As(Exchange.PublicFunctions.ExchangeDocumentInfo.Remote.GetLastDocumentInfo(sendedDocument));
            info.PurchaseOrder = Functions.Module.Remote.GetPurchaseNumber(sendedDocument);
            info.Save();
          }
        }
        catch (Exception ex)
        {
          Logger.Error(Sungero.BulkExchangeSolution.Resources.CannotSendDocument, ex);
        }
      }
    }
    
    /// <summary>
    /// Импортировать неформализованный документ.
    /// </summary>
    /// <param name="file">Путь к файлу.</param>
    /// <returns>Документ.</returns>
    /// <remarks>Работает с локальными путями клиента, не для веб-клиента.</remarks>
    public virtual Docflow.IAccountingDocumentBase ImportNonformalizedDocument(string file)
    {
      var content = System.IO.File.ReadAllBytes(file);
      var array = Docflow.Structures.Module.ByteArray.Create(content);
      var contractStatement = FinancialArchive.ContractStatements.Null;
      using (var memory = new System.IO.MemoryStream(array.Bytes))
      {
        // Создать версию. Сохранить в версию.
        var fileInfo = new FileInfo(file);
        var fileName = fileInfo.Name;
        if (fileName.ToLowerInvariant().Contains("акт"))
        {
          contractStatement = Functions.Module.Remote.CreateContractStatement();
          contractStatement.CreateVersion();
          var version = contractStatement.LastVersion;
          version.AssociatedApplication = Sungero.Exchange.PublicFunctions.Module.Remote.GetOrCreateAssociatedApplicationByDocumentName(fileName);
          version.Body.Write(memory);
          contractStatement.State.Properties.Counterparty.IsRequired = false;
          contractStatement.State.Properties.BusinessUnit.IsRequired = false;
          contractStatement.Save();
        }
      }
      
      return contractStatement;
    }
    
    /// <summary>
    /// Обработать импортированные документы.
    /// </summary>
    /// <param name="documents">Документы.</param>
    /// <param name="responsible">Ответственный за документ.</param>
    /// <param name="titleSignatory">Подписывающий титул продавца.</param>
    /// <param name="isContractStatement">Признак нетоварного потока.</param>
    public virtual void ProcessImportedDocuments(List<IOfficialDocument> documents, Company.IEmployee responsible, Company.IEmployee titleSignatory, bool isContractStatement)
    {
      foreach (var document in documents)
      {
        var accountingDocument = Docflow.AccountingDocumentBases.As(document);
        var isFormalized = accountingDocument.Counterparty != null;
        
        if (!isFormalized)
        {
          var documentWithCounterparty = documents.Where(d => AccountingDocumentBases.As(d).Counterparty != null).FirstOrDefault();
          var counterparty = AccountingDocumentBases.As(documentWithCounterparty).Counterparty;
          accountingDocument.Counterparty = counterparty;
          accountingDocument.Save();
        }
        var signatory = isFormalized ? titleSignatory : null;
        Functions.Module.Remote.ProcessImportedDocument(accountingDocument, responsible, signatory, isContractStatement);
      }
    }
    
    /// <summary>
    /// Обработать документы нетоварного потока.
    /// </summary>
    /// <param name="contractNumbers">Коллекция номеров договоров.</param>
    /// <param name="documents">Документы.</param>
    /// <param name="signatory">Полписант.</param>
    public virtual void ProcessContractStatementDocuments(List<string> contractNumbers, List<IOfficialDocument> documents, Company.IEmployee signatory)
    {
      var contractNumber = contractNumbers.FirstOrDefault();
      var contract = Functions.Module.Remote.GetContractByNumber(contractNumber);
      var businessUnitBoxs = documents.Where(d => Docflow.AccountingDocumentBases.Is(d) && Docflow.AccountingDocumentBases.As(d).BusinessUnitBox != null)
        .Select(d => Docflow.AccountingDocumentBases.As(d).BusinessUnitBox).Distinct();
        
      foreach (var doc in documents)
      {
        var accountingDocument = Docflow.AccountingDocumentBases.As(doc);
        
        if (!string.IsNullOrEmpty(contractNumber))
        {
          var contractNumberString = Module.Exchange.Resources.ContractNumberFormat(contractNumber);
          if (string.IsNullOrWhiteSpace(accountingDocument.Note))
            accountingDocument.Note = contractNumberString;
          else
            accountingDocument.Note += Environment.NewLine + contractNumberString;
        }
        
        if (contract != null)
          accountingDocument.LeadingDocument = contract;
        
        if (accountingDocument.BusinessUnitBox == null && businessUnitBoxs.Count() == 1)
          accountingDocument.BusinessUnitBox = businessUnitBoxs.FirstOrDefault();
        
        accountingDocument.OurSignatory = signatory;
        accountingDocument.Save();
      }
    }
    
    /// <summary>
    /// Обработать документы товарного потока.
    /// </summary>
    /// <param name="documents">Документы.</param>
    public virtual void ProcessWaybillDocuments(List<IOfficialDocument> documents)
    {
      foreach (var doc in documents)
      {
        var PurchaseNumber =  Module.Exchange.Resources.PONumberFormat(Functions.Module.Remote.GetPurchaseNumber(doc));
        if (!string.IsNullOrEmpty(PurchaseNumber))
        {
          if (string.IsNullOrWhiteSpace(doc.Note))
            doc.Note = PurchaseNumber;
          else
            doc.Note += Environment.NewLine + PurchaseNumber;
        }
        doc.Save();
      }
    }
    
    /// <summary>
    /// Определить что документ формализованный по расширению имени файла.
    /// </summary>
    /// <param name="filesPath">Путь к файлу.</param>
    /// <returns>True если расширение файла xml.</returns>
    public virtual bool IsFormalized(string filesPath)
    {
      var fileInfo = new FileInfo(filesPath);
      var fileExtestion = fileInfo.Extension;
      return Equals(fileExtestion, ".xml");
    }
    
    /// <summary>
    /// Получить нашу организацию из документов.
    /// </summary>
    /// <param name="documents">Документы.</param>
    /// <returns>Наша организация.</returns>
    public virtual Company.IBusinessUnit GetBusinessUnit(List<IOfficialDocument> documents)
    {
      var documentsWithBusinessUnit = documents.Where(d => AccountingDocumentBases.As(d).BusinessUnit != null).FirstOrDefault();
      return documentsWithBusinessUnit == null ?
        null :
        AccountingDocumentBases.As(documentsWithBusinessUnit).BusinessUnit;
    }
    
    /// <summary>
    /// Получить руководителя организации из документов.
    /// </summary>
    /// <param name="documents">Документы.</param>
    /// <returns>Руководитель организации.</returns>
    public virtual Company.IEmployee GetChiefBusinessUnit(List<IOfficialDocument> documents)
    {
      var documentsWithBusinessUnit = documents.Where(d => AccountingDocumentBases.As(d).BusinessUnit != null).FirstOrDefault();
      return documentsWithBusinessUnit == null ?
        null :
        AccountingDocumentBases.As(documentsWithBusinessUnit).BusinessUnit.CEO;
    }
    
    /// <summary>
    /// Добавить полученный из файла номер заказа/договора в коллекцию номеров заказов или коллекцию номеров договоров.
    /// </summary>
    /// <param name="filesPath">Путь к файлу.</param>
    /// <param name="purchaseNumbers">Коллекция номеров заказов.</param>
    /// <param name="contractNumbers">Коллекция номеров договоров.</param>
    public virtual void CollectPurchaseAndContractNumbers(string filesPath, List<string> purchaseNumbers, List<string> contractNumbers)
    {
      var file = File.ReadAllBytes(filesPath);
      var byteArray = Docflow.Structures.Module.ByteArray.Create(file);
      var purchaseOrderNumber = Functions.Module.Remote.GetPurchaseNumber(byteArray);
      purchaseNumbers.Add(purchaseOrderNumber);
      if (!string.IsNullOrEmpty(purchaseOrderNumber))
        return;
      
      var contractNumber = Functions.Module.Remote.GetContractNumber(byteArray);
      if (this.IsFormalized(filesPath))
        contractNumbers.Add(contractNumber);
    }
    
    /// <summary>
    /// Проверить полученные номера заказов или номера договоров.
    /// </summary>
    /// <param name="purchaseNumbers">Коллекция номеров заказов.</param>
    /// <param name="contractNumbers">Коллекция номеров договоров.</param>
    /// <returns>True если 1 уникальный номер заказа или договора, и номера заказа и договора не присутствуют одновременно.</returns>
    public virtual bool ValidatePurchaseAndContractNumbers(List<string> purchaseNumbers, List<string> contractNumbers)
    {
      var uniquePurchaseNumbers = purchaseNumbers.Distinct();
      var uniqueContractNumbers = contractNumbers.Distinct();
      var isValid = (uniquePurchaseNumbers.Count() == 1 && !string.IsNullOrEmpty(uniquePurchaseNumbers.Single())) ^
        (uniqueContractNumbers.Count() == 1 && !string.IsNullOrEmpty(uniqueContractNumbers.Single()));
      
      return isValid;
    }
    
    /// <summary>
    /// Импортировать документ.
    /// </summary>
    /// <param name="filesPath">Путь к файлу.</param>
    /// <returns>Документ.</returns>
    public virtual IAccountingDocumentBase ImportDocument(string filesPath)
    {
      var document = Docflow.AccountingDocumentBases.Null;
      var isFormalized = this.IsFormalized(filesPath);
      if (isFormalized)
      {
        Logger.DebugFormat("Import formalized document from path {0}.", filesPath);
        document = FinancialArchive.PublicFunctions.Module.ImportFormalizedDocument(filesPath, false);
        document.Save();
      }
      else
      {
        Logger.DebugFormat("Import nonformalized document from path {0}.", filesPath);
        document = this.ImportNonformalizedDocument(filesPath);
        if (document != null)
          document.Save();
        else
        {
          Logger.DebugFormat("Import nonformalized document from path {0} failed.", filesPath);
          return document;
        }
      }
      Logger.DebugFormat("Document imported with Id {0}.", document.Id);
      Logger.DebugFormat("Process document with Id {0}.", document.Id);
      
      return document;
    }
    
    /// <summary>
    /// Создать и стартовать задачу на согласование по регламенту.
    /// </summary>
    /// <param name="documents">Документы.</param>
    /// <param name="author">Автор задачи.</param>
    /// <param name="signatory">Подписант задачи.</param>
    /// <returns>True, если задача была стартована.</returns>
    public virtual bool CreateAndStartApprovalTask(List<IOfficialDocument> documents, Company.IEmployee author, Company.IEmployee signatory)
    {
      var schf = Docflow.AccountingDocumentBase.FormalizedFunction.Schf;
      var contractStatement = documents.FirstOrDefault(d => AccountingDocumentBases.As(d).FormalizedFunction != schf);
      var totalAmounts = documents.Where(d => AccountingDocumentBases.As(d).TotalAmount != null)
        .Select(d => AccountingDocumentBases.As(d).TotalAmount)
        .Distinct()
        .ToList();
      if (contractStatement != null && totalAmounts.Count == 1)
      {
        var task = Docflow.PublicFunctions.Module.Remote.CreateApprovalTask(contractStatement);
        task.Author = author;
        task.Signatory = signatory;
        task.Start();
        return true;
      }
      
      return false;
    }

    #endregion

    #region Подписание и отказ по товарным комплектам

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
              else
              {
                Logger.DebugFormat("Cannot sign document set with document ids {0}.", string.Join(", ", info.Document.Id));
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

    #endregion
    
    #region Действия списков на подписание
    
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

      var certificate = businessUnitBox != null ? Sungero.Exchange.PublicFunctions.Module.GetUserExchangeCertificate(businessUnitBox, currentEmployee) : null;
      
      if (businessUnitBox != null && certificate == null)
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
    
    public void ShowApprovalSigningAssignments(IAccountingDocumentBase document)
    {
      var assignments = Functions.Module.Remote.GetApprovalSigningAssignments(document);
      if (assignments.Count() == 1)
        assignments.FirstOrDefault().ShowModal();
      else
        assignments.ShowModal();
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
        var documents = assignments.SelectMany(x => x.DocumentGroup.OfficialDocuments).Select(a => OfficialDocuments.As(a)).ToList();
        var addendaGroupDocuments = assignments.SelectMany(x => x.AddendaGroup.OfficialDocuments).Select(a => OfficialDocuments.As(a)).ToList();
        documents.AddRange(addendaGroupDocuments);
        documents.ShowModal();
      }
      else
        Dialogs.ShowMessage(Resources.NoAssignmentError, MessageType.Error);
    }
    
    private static void ShowResultDialog(List<string> textList)
    {
      var text = string.Join(Environment.NewLine, textList);
      Dialogs.ShowMessage(Resources.DoumentsSignError, text, MessageType.Error);
    }
    
    #endregion
    
  }
}