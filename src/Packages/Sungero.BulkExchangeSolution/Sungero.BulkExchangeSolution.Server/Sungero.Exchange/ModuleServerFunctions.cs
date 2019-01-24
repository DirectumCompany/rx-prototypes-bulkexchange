using System;
using System.Collections.Generic;
using System.Linq;
using NpoComputer.DCX.Common;
using Sungero.BulkExchangeSolution.ExchangeDocumentInfo;
using Sungero.BulkExchangeSolution.Structures.Exchange.ExchangeDocumentInfo;
using Sungero.FinancialArchive;
using Sungero.Commons;
using Sungero.Core;
using Sungero.CoreEntities;
using Sungero.Docflow;
using Sungero.ExchangeCore;
using Sungero.Parties;
using Sungero.Workflow;
using SignStatus = Sungero.BulkExchangeSolution.ExchangeDocumentInfo.SignStatus;

namespace Sungero.BulkExchangeSolution.Module.Exchange.Server
{
  partial class ModuleFunctions
  {
    protected override Sungero.Docflow.IOfficialDocument GetOrCreateNewExchangeDocument(object documentUntyped, Sungero.ExchangeCore.IBoxBase box, Sungero.Parties.ICounterparty sender, bool isIncoming, string serviceCounterpartyId, DateTime messageDate)
    {
      var serviceDocument = documentUntyped as NpoComputer.DCX.Common.IDocument;
      var document = base.GetOrCreateNewExchangeDocument(documentUntyped, box, sender, isIncoming, serviceCounterpartyId, messageDate);

      var accountingDocument = AccountingDocumentBases.As(document);
      
      if (accountingDocument != null && accountingDocument.IsFormalized == true && (UniversalTransferDocuments.Is(document) ||
                                                                                    IncomingTaxInvoices.Is(document) || Waybills.Is(document) || ContractStatements.Is(document)))
      {
        var xdoc = System.Xml.Linq.XDocument.Load(new System.IO.MemoryStream(serviceDocument.Content));
        RemoveNamespaces(xdoc);
        var additionalProperties = xdoc.Descendants("ТекстИнф").ToList();
        
        // В ДПРР это может быть другой xml элемент.
        var contractStatementAdditionalProperties = xdoc.Descendants("ИнфПолФХЖ2").ToList();
        if (contractStatementAdditionalProperties.Any())
          additionalProperties.AddRange(contractStatementAdditionalProperties);
        
        if (additionalProperties.Any())
        {
          var exchangeDocumentInfo = ExchangeDocumentInfos.As(Sungero.Exchange.PublicFunctions.ExchangeDocumentInfo.GetExDocumentInfoByExternalId(box, serviceDocument.ServiceEntityId));
          
          var purchaseOrderElement = additionalProperties.FirstOrDefault(i => (string)i.Attribute("Идентиф") == Constants.Module.PurchaseOrder);
          if (purchaseOrderElement != null)
            exchangeDocumentInfo.PurchaseOrder = purchaseOrderElement.Attribute("Значен").Value;
          
          var contractStatementElement = additionalProperties.FirstOrDefault(i => (string)i.Attribute("Идентиф") == Constants.Module.ContractNumber);
          if (contractStatementElement != null)
            exchangeDocumentInfo.ContractNumber = contractStatementElement.Attribute("Значен").Value;
          
          if (purchaseOrderElement != null || contractStatementElement != null)
            exchangeDocumentInfo.Save();
        }
      }
      else if (serviceDocument.FileName.ToLowerInvariant().Contains("акт"))
      {
        var pattern = @"(^|[^А-Яа-я])акт([^А-Яа-я]|$)";
        if (System.Text.RegularExpressions.Regex.IsMatch(document.Name.ToLower(), pattern))
        {
          var noteArray  = document.Note.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
          var index = Array.IndexOf(noteArray, Constants.Module.ContractNumber);
          var contractNumber = noteArray.ElementAtOrDefault(index + 1);
          
          if (!string.IsNullOrEmpty(contractNumber))
          {
            var exchangeDocumentInfo = ExchangeDocumentInfos.As(Sungero.Exchange.PublicFunctions.ExchangeDocumentInfo.GetExDocumentInfoByExternalId(box, serviceDocument.ServiceEntityId));
            exchangeDocumentInfo.ContractNumber = contractNumber;
            document.Subject = "Выполнение услуг";
            document.Note = serviceDocument.Comment;
            document.Save();
            exchangeDocumentInfo.Save();
          }
        }
      }
      return document;
    }
    
    public virtual List<Sungero.BulkExchangeSolution.Structures.Exchange.ExchangeDocumentInfo.DocumentSet> GetSignedAndNotSendedDocumentSets()
    {
      var boxes = Sungero.ExchangeCore.PublicFunctions.BusinessUnitBox.Remote.GetConnectedBoxes().ToList();
      
      var infos = ExchangeDocumentInfos.GetAll()
        .Where(x => boxes.Contains(x.RootBox)
               && x.VerificationStatus == ExchangeDocumentInfo.VerificationStatus.Completed
               && x.ExchangeState == ExchangeDocumentInfo.ExchangeState.SignRequired
               && x.SignStatus == ExchangeDocumentInfo.SignStatus.Signed
               && x.ReceiverSignId == null);
      
      return BulkExchangeSolution.Functions.ExchangeDocumentInfo.GetDocumentSets(infos.ToList()).Where(s => s.IsFullSet).ToList();
    }
    
    public override Sungero.Exchange.Structures.Module.DocumentCertificatesInfo GetDocumentCertificatesToBox(IOfficialDocument document, Sungero.ExchangeCore.IBusinessUnitBox box)
    {
      return base.GetDocumentCertificatesToBox(document, box);
    }
    
    /// <summary>
    /// Связать документы.
    /// </summary>
    /// <param name="document">Документ.</param>
    /// <param name="relatedExchangeDocInfo">Информация о связываемом документе обмена.</param>
    public override void AddRelations(Docflow.IOfficialDocument document, Sungero.Exchange.IExchangeDocumentInfo relatedExchangeDocInfo)
    {
      var relatedExchangeDocumentInfo = Sungero.BulkExchangeSolution.ExchangeDocumentInfos.As(relatedExchangeDocInfo);
      var documentSet = Sungero.BulkExchangeSolution.Functions.ExchangeDocumentInfo.GetDocumentSet(relatedExchangeDocumentInfo);
      if (documentSet == null || !documentSet.IsFullSet)
        base.AddRelations(document, relatedExchangeDocInfo);
    }

    /// <summary>
    /// Проверка документов по учетной системе.
    /// </summary>
    /// <param name="documentSet">Комплект документов.</param>
    public void VerifyDocumentSet(Sungero.BulkExchangeSolution.Structures.Exchange.ExchangeDocumentInfo.DocumentSet documentSet)
    {
      if (documentSet.Type != BulkExchangeSolution.Constants.Exchange.ExchangeDocumentInfo.DocumentSetType.Waybill)
        return;
      
      var totalAmount = Sungero.Docflow.AccountingDocumentBases.As(documentSet.ExchangeDocumentInfos.FirstOrDefault().Document).TotalAmount;
      var result = true;
      var reason = string.Empty;
      var documents = new List<IOfficialDocument>();
      
      foreach (var info in documentSet.ExchangeDocumentInfos)
      {
        documents.Add(info.Document);
        var document = AccountingDocumentBases.As(info.Document);
        if (document.TotalAmount != totalAmount)
        {
          result = false;
          reason = Resources.DocumentsTotalAmountError;
        }

        var rubCurrency = Currencies.GetAll().FirstOrDefault(x =>
                                                             string.Equals(x.AlphaCode, Sungero.BulkExchangeSolution.Module.Exchange.Resources.RubAlphaCode,
                                                                           StringComparison.InvariantCultureIgnoreCase));
        
        if (!Equals(document.Currency, rubCurrency) || document.TotalAmount >= Constants.Module.DocumentMaxTotalAmount)
        {
          result = false;
          reason = Resources.TotalAmountIsTooBig;
        }
      }

      var logMessage = Resources.DocumentSetWithIDs.ToString();
      for (int i = 0; i < documents.Count; i++)
        logMessage += i == 0 ? documents[i].Id.ToString() : ", " + documents[i].Id;
      
      var documentInfo = documentSet.ExchangeDocumentInfos.FirstOrDefault(x => Sungero.FinancialArchive.UniversalTransferDocuments.Is(x.Document));
      var task = documentInfo.VerificationTask;
      if (!result)
      {
        if ((task == null || task != null && task.Status == Workflow.Task.Status.Completed) &&
            this.IsDocumentVerificationCompleted(documentInfo))
          result = true;
      }
      
      logMessage += result ? Resources.VerificationSuccess : Resources.VerificationFail + reason;
      Logger.Debug(logMessage);
      
      this.SetVerificationResult(documentSet, result, reason);
      foreach (var info in documentSet.ExchangeDocumentInfos.Where(i => i.VerificationStatus == VerificationStatus.Completed))
        this.GenerateDefaultTitle(info.Document);
      
      this.SendDocumentProcessingTask(documentSet, result);
    }
    
    public override void StartExchangeTask(IBoxBase box, object messageUntyped, ICounterparty sender, bool isIncoming, string exchangeTaskActiveTextBoundedDocuments, List<Sungero.Exchange.IExchangeDocumentInfo> infos)
    {
      var message = messageUntyped as NpoComputer.DCX.Common.IMessage;
      var exchangeDocumentInfos = Sungero.BulkExchangeSolution.ExchangeDocumentInfos.GetAll().Where(e => e.ServiceMessageId == message.ServiceMessageId).ToList();
      if (exchangeDocumentInfos.Any(i => i.VerificationStatus == VerificationStatus.Required))
        return;
      
      var documentSets = Sungero.BulkExchangeSolution.Functions.ExchangeDocumentInfo.GetDocumentSets(exchangeDocumentInfos);
      if (documentSets.Any(s => s.IsFullSet && s.Type == BulkExchangeSolution.Constants.Exchange.ExchangeDocumentInfo.DocumentSetType.ContractStatement))
        return;
      
      base.StartExchangeTask(box, messageUntyped, sender, isIncoming, exchangeTaskActiveTextBoundedDocuments, infos);
    }
    
    private bool IsDocumentVerificationCompleted(IExchangeDocumentInfo document)
    {
      return document != null && (document.ExchangeState == Sungero.Exchange.ExchangeDocumentInfo.ExchangeState.Signed || document.ExchangeState == Sungero.Exchange.ExchangeDocumentInfo.ExchangeState.Obsolete ||
                                  document.ExchangeState == Sungero.Exchange.ExchangeDocumentInfo.ExchangeState.Rejected || document.ExchangeState == Sungero.Exchange.ExchangeDocumentInfo.ExchangeState.Terminated ||
                                  string.Equals(document.Document.Note.Trim(), "проведено", StringComparison.InvariantCultureIgnoreCase));
    }
    
    protected override void ProcessDocumentsFromNewIncomingMessage(List<Sungero.Exchange.IExchangeDocumentInfo> infos,
                                                                   object messageUntyped,
                                                                   IBoxBase box,
                                                                   ICounterparty sender,
                                                                   IMessageQueueItem queueItem,
                                                                   bool isIncoming,
                                                                   object untypedProcessingDocuments)
    {
      var documentSet = this.GetDocumentSet(messageUntyped);
      if (documentSet != null)
      {
        var isFullSet = documentSet.IsFullSet;
        this.SetStatuses(documentSet.ExchangeDocumentInfos, isFullSet, documentSet.Type);
        this.ProcessDocumenSetFromNewIncomingMessage(documentSet);
        
        if (isFullSet)
        {
          Functions.Module.VerifyDocumentSet(documentSet);
          this.SendContractStatementForApproval(documentSet);
        }
      }
      base.ProcessDocumentsFromNewIncomingMessage(infos, messageUntyped, box, sender, queueItem, isIncoming, untypedProcessingDocuments);
    }
    
    /// <summary>
    /// Обработать бухгалтерские документы в комплекте.
    /// </summary>
    /// <param name="documentSet">Комплект документов.</param>
    public virtual void ProcessDocumenSetFromNewIncomingMessage(Sungero.BulkExchangeSolution.Structures.Exchange.ExchangeDocumentInfo.DocumentSet documentSet)
    {
      if (documentSet == null)
        return;
      
      var counterparty = documentSet.ExchangeDocumentInfos.Select(i => i.Counterparty).Distinct().Single();
      var responsible = CompanyBases.Is(counterparty) ? CompanyBases.As(counterparty).Responsible : null;
      var caseFile = Docflow.CaseFiles.GetAll(c => c.Status == Docflow.CaseFile.Status.Active).FirstOrDefault();
      
      foreach (var info in documentSet.ExchangeDocumentInfos)
      {
        var accountingDocument = Docflow.AccountingDocumentBases.As(info.Document);
        if (accountingDocument == null)
          continue;
        
        if (documentSet.Type != BulkExchangeSolution.Constants.Exchange.ExchangeDocumentInfo.DocumentSetType.ContractStatement &&
            documentSet.Type != BulkExchangeSolution.Constants.Exchange.ExchangeDocumentInfo.DocumentSetType.Waybill)
          continue;
        
        // Заполнить договор для акта.
        if (documentSet.Type == BulkExchangeSolution.Constants.Exchange.ExchangeDocumentInfo.DocumentSetType.ContractStatement &&
            !string.IsNullOrEmpty(info.ContractNumber))
        {
          var contract = Contracts.ContractBases.GetAll(c => Equals(accountingDocument.Counterparty, c.Counterparty) && c.RegistrationNumber == info.ContractNumber).FirstOrDefault();
          accountingDocument.LeadingDocument = contract;
        }
        
        if (documentSet.IsFullSet == true)
        {
          // Заполнить номенклатуру дела.
          if (caseFile != null)
            accountingDocument.CaseFile = caseFile;
          
          // Заполнить ответственного и подразделение.
          if (responsible != null)
          {
            if (accountingDocument.Department == null)
            {
              // Подразделение зарегистрированного документа можно менять только на смене рег.данных.
              var entityParams = (accountingDocument as Domain.Shared.IExtendedEntity).Params;
              entityParams[Constants.Module.RepeatRegister] = true;
              accountingDocument.Department = responsible.Department;
            }
            
            // Не у всех типов доступен ответственный на карточке.
            if (accountingDocument.ResponsibleEmployee == null && accountingDocument.State.Properties.ResponsibleEmployee.IsVisible)
              accountingDocument.ResponsibleEmployee = responsible;

            // Выдать права на документ ответственному за контрагента.
            GrantAccessRightsForResponsible(accountingDocument, responsible);
          }
        }
        
        // Сохранить документ, если были изменения.
        if (accountingDocument.State.IsChanged)
          accountingDocument.Save();
      }
      if (documentSet.IsFullSet == true && documentSet.ExchangeDocumentInfos.Count == 2)
        this.AddRelationsForDocumentSet(documentSet);
    }

    /// <summary>
    /// Создать документ обмена.
    /// </summary>
    /// <param name="fileName">Имя файла.</param>
    /// <param name="comment">Комментарий.</param>
    /// <param name="box">Ящик обмена.</param>
    /// <param name="counterparty">Контрагент.</param>
    /// <param name="info">Информация о документе.</param>
    /// <returns>Созданный документ.</returns>
    public override IOfficialDocument CreateExchangeDocument(string fileName, string comment, IBoxBase box, ICounterparty counterparty, Sungero.Exchange.IExchangeDocumentInfo info)
    {
      if (fileName.ToLowerInvariant().Contains("акт") && comment.ToLowerInvariant().Contains("номер_договора"))
      {
        var contractStatement = FinancialArchive.ContractStatements.Create();
        contractStatement.Note = comment;
        contractStatement.BusinessUnit = ExchangeCore.PublicFunctions.BoxBase.GetBusinessUnit(box);
        contractStatement.BusinessUnitBox = ExchangeCore.PublicFunctions.BoxBase.GetRootBox(box);
        contractStatement.Counterparty = counterparty;
        contractStatement.AccessRights.Grant(ExchangeCore.PublicFunctions.BoxBase.GetExchangeDocumentResponsible(box, counterparty,  new List<Sungero.Exchange.IExchangeDocumentInfo>() { info }), DefaultAccessRightsTypes.FullAccess);
        contractStatement.IsFormalized = false;
        return contractStatement;
      }
      return base.CreateExchangeDocument(fileName, comment, box, counterparty, info);
    }

    /// <summary>
    /// Создать связь документов комплекта.
    /// </summary>
    /// <param name="documentSet">Комплект.</param>
    private void AddRelationsForDocumentSet(Sungero.BulkExchangeSolution.Structures.Exchange.ExchangeDocumentInfo.DocumentSet documentSet)
    {
      var exchangeDocuments = documentSet.ExchangeDocumentInfos.Select(e => e.Document).ToList();
      var firstDocument = OfficialDocuments.As(exchangeDocuments[0]);
      var secondDocument = OfficialDocuments.As(exchangeDocuments[1]);
      
      var accountingDocument = AccountingDocumentBases.As(firstDocument);
      if (accountingDocument != null && accountingDocument.FormalizedFunction == Docflow.AccountingDocumentBase.FormalizedFunction.Schf)
      {
        secondDocument.Relations.AddOrUpdate(Sungero.Exchange.Constants.Module.AddendumRelationName, null, firstDocument);
        secondDocument.Save();
      }
      else
      {
        firstDocument.Relations.AddOrUpdate(Sungero.Exchange.Constants.Module.AddendumRelationName, null, secondDocument);
        firstDocument.Save();
      }
    }
    
    /// <summary>
    /// Отправлять задания/уведомления ответственному.
    /// </summary>
    /// <param name="box">Абонентский ящик.</param>
    /// <param name="messageUntyped">Сообщение.</param>
    /// <returns>Признак отправки задания ответственному за ящик/контрагента.</returns>
    protected override bool NeedReceiveTask(IBoxBase box, object messageUntyped)
    {
      var documentSet = this.GetDocumentSet(messageUntyped);
      var isFullSet = documentSet != null && documentSet.IsFullSet;
      
      return base.NeedReceiveTask(box, messageUntyped) && !isFullSet;
    }
    
    protected virtual void GrantAccessRightsForResponsible(IOfficialDocument document, Company.IEmployee responsible)
    {
      if (!document.AccessRights.IsGranted(DefaultAccessRightsTypes.FullAccess, responsible))
      {
        document.AccessRights.Grant(responsible, DefaultAccessRightsTypes.FullAccess);
        document.AccessRights.Save();
      }
    }
    
    protected override void ProcessMessageError(object clientUntyped, List<int> queueItemsIds, object messageUntyped, string exception)
    {
      base.ProcessMessageError(clientUntyped, queueItemsIds, messageUntyped, exception);
      
      var regexMatch = System.Text.RegularExpressions.Regex.Match(exception, "^#([0-9])+:", System.Text.RegularExpressions.RegexOptions.Compiled);
      if (regexMatch.Success)
      {
        var client = clientUntyped as NpoComputer.DCX.ClientApi.Client;
        var message = messageUntyped as NpoComputer.DCX.Common.IMessage;
        
        var queueItems = ExchangeCore.MessageQueueItems.GetAll(q => queueItemsIds.Contains(q.Id)).ToList();
        var queueItem = queueItems.Single(x => x.ExternalId == message.ServiceMessageId);
        
        var box = queueItem.Box;
        var businessUnitBox = queueItem.RootBox;
        
        var organizationId = message.Sender.Organization.OrganizationId;
        var isIncoming = true;
        
        // Обрабатываем исходящие сообщения для поддержки параллельных действий.
        if (organizationId == businessUnitBox.OrganizationId)
        {
          organizationId = message.Receiver.Organization.OrganizationId;
          isIncoming = false;
        }
        
        var sender = Parties.Counterparties.GetAll(c => c.ExchangeBoxes.Any(e => Equals(e.OrganizationId, organizationId) && Equals(businessUnitBox, e.Box))).SingleOrDefault();
        
        var code = int.Parse(regexMatch.Groups[1].Value);
        if (code == 1)
        {
          var task = SimpleTasks.Create("Необходимо заполнить ответственного за контрагента " + sender.Name, box.Responsible);
          task.Start();
        }
      }
    }
    
    private void SendDocumentProcessingTask(DocumentSet documentSet, bool verificationResult)
    {
      var documentInfo =
        documentSet.ExchangeDocumentInfos.FirstOrDefault(x =>
                                                         Sungero.FinancialArchive.UniversalTransferDocuments.Is(x.Document));
      var createTime = documentSet.ExchangeDocumentInfos.Select(x => x.Document.Created).Max();
      
      if ((documentInfo.VerificationTask == null || documentInfo.VerificationTask.Status != Workflow.Task.Status.InProcess) &&
          Calendar.Now - createTime > TimeSpan.FromHours(Constants.Module.DocumentVerificationDeadlineInHours) && !verificationResult)
      {
        var client =
          ExchangeCore.PublicFunctions.BusinessUnitBox.GetPublicClient(documentInfo.RootBox) as
          NpoComputer.DCX.ClientApi.Client;
        var message = client.GetMessage(documentInfo.ServiceMessageId);
        var isIncoming = message.Sender.Organization.OrganizationId != documentInfo.RootBox.OrganizationId;

        var taskText = Environment.NewLine + Sungero.BulkExchangeSolution.Module.Exchange.Resources.VerificationFailedTaskText +
          documentInfo.VerificationFailReason;
        var processingTask = this.CreateExchangeTask(documentInfo.RootBox, message, documentInfo.Counterparty, isIncoming, taskText,
                                                     documentSet.ExchangeDocumentInfos.Select(x => Sungero.Exchange.ExchangeDocumentInfos.As(x)).ToList());
        processingTask.Start();
        documentInfo.VerificationTask = processingTask;
        documentInfo.Save();
      }
    }
    
    private void SetVerificationResult(DocumentSet documentSet, bool result, string reason)
    {
      foreach (var info in documentSet.ExchangeDocumentInfos)
      {
        if (result)
        {
          info.VerificationStatus = VerificationStatus.Completed;
          info.VerificationFailReason = null;
        }
        else
        {
          info.VerificationFailReason = reason;
          info.VerificationStatus = VerificationStatus.Required;
        }

        info.Save();
      }
    }
    
    /// <summary>
    /// Получить комплект документов.
    /// </summary>
    /// <param name="messageUntyped">Сообщение.</param>
    /// <returns>Структура с комплектом - признак полноты и инфошки. Может быть null, если в сообщении нет никаких признаков комплекта.</returns>
    private Sungero.BulkExchangeSolution.Structures.Exchange.ExchangeDocumentInfo.DocumentSet GetDocumentSet(object messageUntyped)
    {
      var message = messageUntyped as NpoComputer.DCX.Common.IMessage;
      var exchangeDocumentInfo = Sungero.BulkExchangeSolution.ExchangeDocumentInfos.GetAll().Where(e => e.ServiceMessageId == message.ServiceMessageId).FirstOrDefault();
      return exchangeDocumentInfo != null
        ? Sungero.BulkExchangeSolution.Functions.ExchangeDocumentInfo.GetDocumentSet(exchangeDocumentInfo)
        : null;
    }
    
    /// <summary>
    /// Установить статусы отказа и сверки.
    /// </summary>
    /// <param name="documentInfos">Список информаций о документах обмена.</param>
    /// <param name="isFullSet">True если комплект полный.</param>
    private void SetStatuses(List<IExchangeDocumentInfo> documentInfos, bool isFullSet, string documentSetType)
    {
      var rejectionStatus = !isFullSet && documentSetType == BulkExchangeSolution.Constants.Exchange.ExchangeDocumentInfo.DocumentSetType.Waybill
        ? ExchangeDocumentInfo.RejectionStatus.Required
        : ExchangeDocumentInfo.RejectionStatus.NotRequired;

      var verificationStatus = isFullSet
        ? ExchangeDocumentInfo.VerificationStatus.Required
        : ExchangeDocumentInfo.VerificationStatus.NotRequired;
      
      foreach (var exhangeDoc in documentInfos)
      {
        exhangeDoc.RejectionStatus = rejectionStatus;
        exhangeDoc.VerificationStatus = verificationStatus;
        exhangeDoc.Save();
      }
    }
    /// <summary>
    /// Сгенерировать титулы.
    /// </summary>
    /// <param name="document">Документ.</param>
    private void GenerateDefaultTitle(Docflow.IOfficialDocument document)
    {
      if (!AccountingDocumentBases.Is(document))
        return;
      
      var accountDocument = AccountingDocumentBases.As(document);
      if (accountDocument.ExchangeState == Docflow.OfficialDocument.ExchangeState.SignRequired && accountDocument.BuyerTitleId == null)
        Docflow.PublicFunctions.AccountingDocumentBase.Remote.GenerateDefaultAnswer(accountDocument, document.BusinessUnit.CEO, true);
    }
    
    /// <summary>
    /// Отправить комплект документов по услугам на согласование по регламенту.
    /// </summary>
    /// <param name="documentSet">Комплект.</param>
    private void SendContractStatementForApproval(Sungero.BulkExchangeSolution.Structures.Exchange.ExchangeDocumentInfo.DocumentSet documentSet)
    {
      if (documentSet.Type != BulkExchangeSolution.Constants.Exchange.ExchangeDocumentInfo.DocumentSetType.ContractStatement)
        return;
      
      var contractStatement = documentSet.ExchangeDocumentInfos.Select(i => i.Document).First(d => FinancialArchive.ContractStatements.Is(d) ||
                                                                                              FinancialArchive.UniversalTransferDocuments.Is(d));
      var task = Docflow.PublicFunctions.Module.Remote.CreateApprovalTask(contractStatement);
      var counterparty = Docflow.AccountingDocumentBases.As(contractStatement).Counterparty;
      var responsible = CompanyBases.Is(counterparty) ? CompanyBases.As(counterparty).Responsible : null;
      if (responsible != null)
        task.Author = responsible;
      
      task.Start();
    }
  }
}