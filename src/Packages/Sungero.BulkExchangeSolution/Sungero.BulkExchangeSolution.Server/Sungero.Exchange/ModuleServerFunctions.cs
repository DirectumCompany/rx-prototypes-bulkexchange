using System;
using System.Collections.Generic;
using System.Linq;
using NpoComputer.DCX.Common;
using Sungero.BulkExchangeSolution.ExchangeDocumentInfo;
using Sungero.BulkExchangeSolution.Structures.Exchange.ExchangeDocumentInfo;
using Sungero.Commons;
using Sungero.Core;
using Sungero.CoreEntities;
using Sungero.Docflow;
using Sungero.ExchangeCore;
using Sungero.ExchangeCore.PublicFunctions;
using Sungero.FinancialArchive;
using Sungero.Parties;
using Sungero.Workflow;
using SignStatus = Sungero.BulkExchangeSolution.ExchangeDocumentInfo.SignStatus;

namespace Sungero.BulkExchangeSolution.Module.Exchange.Server
{
  partial class ModuleFunctions
  {
    #region Перекрытие получения сообщений

    protected override bool ProcessDocumentsFromNewIncomingMessage(IMessage message,
                                                                   ExchangeCore.IMessageQueueItem queueItem,
                                                                   List<Sungero.Exchange.IExchangeDocumentInfo> infos,
                                                                   List<IDocument> processingDocuments,
                                                                   ICounterparty sender,
                                                                   bool isIncoming,
                                                                   IBoxBase box)
    {
      var queueItems = MessageQueueItems.GetAll(q => Equals(q.RootBox, queueItem.RootBox)).ToList();
      var responsible = BoxBase.GetExchangeDocumentResponsible(box, sender, infos);
      if (responsible == null)
      {
        this.StartSimpleTaskWhenCounterpartyResponsibleNotFound(queueItems, MessageQueueItems.As(queueItem), sender, queueItem.RootBox);
        return false;
      }
      
      var documentSet = this.GetDocumentSet(message);
      if (documentSet != null)
      {
        this.SetStatuses(documentSet);
        this.ProcessDocumenSetFromNewIncomingMessage(documentSet);
        
        if (documentSet.IsFullSet)
          this.SendContractStatementForApproval(documentSet);
      }
      
      return base.ProcessDocumentsFromNewIncomingMessage(message, queueItem, infos, processingDocuments, sender, isIncoming, box);
    }
    
    protected override IOfficialDocument GetOrCreateNewExchangeDocument(IDocument document, ICounterparty sender, string serviceCounterpartyId, bool isIncoming, DateTime messageDate, IBoxBase box)
    {
      var createdDocument = base.GetOrCreateNewExchangeDocument(document, sender, serviceCounterpartyId, isIncoming, messageDate, box);

      var accountingDocument = AccountingDocumentBases.As(createdDocument);
      
      if (accountingDocument != null && accountingDocument.IsFormalized == true && (UniversalTransferDocuments.Is(createdDocument) ||
                                                                                    IncomingTaxInvoices.Is(createdDocument) || Waybills.Is(createdDocument) || ContractStatements.Is(createdDocument)))
      {
        var additionalProperties = this.GetAdditionalProperties(document.Content);
        
        if (additionalProperties.Any())
        {
          var exchangeDocumentInfo = ExchangeDocumentInfos.As(Sungero.Exchange.PublicFunctions.ExchangeDocumentInfo.GetExDocumentInfoByExternalId(box, document.ServiceEntityId));
          
          var purchaseOrderNumber = this.GetPurchaseOrderNumber(additionalProperties);
          if (!string.IsNullOrEmpty(purchaseOrderNumber))
            exchangeDocumentInfo.PurchaseOrder = purchaseOrderNumber;

          var contractStatementNumber = this.GetContractNumber(additionalProperties);
          if (!string.IsNullOrEmpty(contractStatementNumber))
            exchangeDocumentInfo.ContractNumber = contractStatementNumber;
          
          if (!string.IsNullOrEmpty(purchaseOrderNumber) || !string.IsNullOrEmpty(contractStatementNumber))
            exchangeDocumentInfo.Save();
        }
      }
      else if (document.FileName.ToLowerInvariant().Contains("акт"))
      {
        var pattern = @"(^|[^А-Яа-я])акт([^А-Яа-я]|$)";
        if (System.Text.RegularExpressions.Regex.IsMatch(createdDocument.Name.ToLower(), pattern))
        {
          var noteArray  = createdDocument.Note.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
          var index = Array.IndexOf(noteArray, Constants.Module.ContractNumber);
          var contractNumber = noteArray.ElementAtOrDefault(index + 1);
          
          if (!string.IsNullOrEmpty(contractNumber))
          {
            var exchangeDocumentInfo = ExchangeDocumentInfos.As(Sungero.Exchange.PublicFunctions.ExchangeDocumentInfo.GetExDocumentInfoByExternalId(box, document.ServiceEntityId));
            exchangeDocumentInfo.ContractNumber = contractNumber;
            createdDocument.Subject = "Выполнение услуг";
            createdDocument.Note = document.Comment;
            createdDocument.Save();
            exchangeDocumentInfo.Save();
          }
        }
      }
      return createdDocument;
    }

    /// <summary>
    /// Создать документ обмена.
    /// </summary>
    /// <param name="info">Информация о документе.</param>
    /// <param name="counterparty">Контрагент.</param>
    /// <param name="box">Ящик обмена.</param>
    /// <param name="fileName">Имя файла.</param>
    /// <param name="comment">Комментарий.</param>
    /// <returns>Созданный документ.</returns>
    protected override IOfficialDocument CreateExchangeDocument(Sungero.Exchange.IExchangeDocumentInfo info, ICounterparty counterparty, IBoxBase box, string fileName, string comment)
    {
      if (fileName.ToLowerInvariant().Contains("акт") && comment.ToLowerInvariant().Contains("номер_договора"))
      {
        var contractStatement = FinancialArchive.ContractStatements.Create();
        contractStatement.Note = comment;
        contractStatement.BusinessUnit = ExchangeCore.PublicFunctions.BoxBase.GetBusinessUnit(box);
        contractStatement.BusinessUnitBox = ExchangeCore.PublicFunctions.BoxBase.GetRootBox(box);
        contractStatement.Counterparty = counterparty;
        var responsible = ExchangeCore.PublicFunctions.BoxBase.GetExchangeDocumentResponsible(box, counterparty,  new List<Sungero.Exchange.IExchangeDocumentInfo>() { info });
        if (responsible != null)
          contractStatement.AccessRights.Grant(responsible, DefaultAccessRightsTypes.FullAccess);
        contractStatement.IsFormalized = false;
        return contractStatement;
      }
      
      return base.CreateExchangeDocument(info, counterparty, box, fileName, comment);
    }
    
    protected override bool StartExchangeTask(IMessage message, List<Sungero.Exchange.IExchangeDocumentInfo> infos, ICounterparty sender, bool isIncoming,
                                              IBoxBase box,
                                              string exchangeTaskActiveTextBoundedDocuments)
    {
      var exchangeDocumentInfos = Sungero.BulkExchangeSolution.ExchangeDocumentInfos.GetAll().Where(e => e.ServiceMessageId == message.ServiceMessageId).ToList();
      if (exchangeDocumentInfos.Any(i => i.VerificationStatus == VerificationStatus.Required))
        return true;
      
      var documentSets = Sungero.BulkExchangeSolution.Functions.ExchangeDocumentInfo.GetDocumentSets(exchangeDocumentInfos);
      if (documentSets.Any(s => s.IsFullSet && s.Type == BulkExchangeSolution.Constants.Exchange.ExchangeDocumentInfo.DocumentSetType.ContractStatement))
        return true;
      
      return base.StartExchangeTask(message, infos, sender, isIncoming, box, exchangeTaskActiveTextBoundedDocuments);
    }

    /// <summary>
    /// Отправлять задания/уведомления ответственному.
    /// </summary>
    /// <param name="box">Абонентский ящик.</param>
    /// <param name="message">Сообщение.</param>
    /// <returns>Признак отправки задания ответственному за ящик/контрагента.</returns>
    protected override bool NeedReceiveDocumentProcessingTask(IBoxBase box, IMessage message)
    {
      var documentSet = this.GetDocumentSet(message);
      var isFullSet = documentSet != null && documentSet.IsFullSet;
      var isReject = documentSet != null && documentSet.ExchangeDocumentInfos.Any(i => i.RejectionStatus != RejectionStatus.NotRequired);
      return base.NeedReceiveDocumentProcessingTask(box, message) && !isFullSet && !isReject;
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
    /// Отправить уведомление ответственному о поступлении ответа от контрагента о подписании документа.
    /// </summary>
    /// <param name="box">Абонентский ящик.</param>
    /// <param name="trackingString">Строка выдачи.</param>
    /// <param name="versionNumber">Версия документа.</param>
    /// <param name="versionIsChanged">Признак того, что версия была изменена.</param>
    /// <param name="signed">Признак подписания. True - если документ подписан контрагентом, иначе - false.</param>
    /// <param name="signatoryInfo">Иформация о контрагенте.</param>
    /// <param name="isInvoiceAmendmentRequest">Отправлено уточнение по СФ или УПД.</param>
    /// <param name="serviceName">Наименование сервиса обмена.</param>
    /// <param name="comment">Комментарий.</param>
    protected override void SendDocumentReplyNotice(IBoxBase box, IOfficialDocumentTracking trackingString, int? versionNumber,
                                                    bool versionIsChanged, bool signed,
                                                    string signatoryInfo, bool isInvoiceAmendmentRequest,
                                                    string serviceName, string comment)
    {
      if (signed && !isInvoiceAmendmentRequest)
      {
        var info = Sungero.Exchange.PublicFunctions.ExchangeDocumentInfo.Remote.GetLastDocumentInfo(trackingString.OfficialDocument);
        var documentInfo = ExchangeDocumentInfos.As(info);
        var documentSet = documentInfo != null
          ? Sungero.BulkExchangeSolution.Functions.ExchangeDocumentInfo.GetDocumentSet(documentInfo)
          : null;
        if (documentSet != null &&
            documentSet.Type == BulkExchangeSolution.Constants.Exchange.ExchangeDocumentInfo.DocumentSetType.Waybill &&
            documentInfo.MessageType == ExchangeDocumentInfo.MessageType.Outgoing)
          return;
      }
      
      base.SendDocumentReplyNotice(box, trackingString, versionNumber, versionIsChanged, signed, signatoryInfo, isInvoiceAmendmentRequest, serviceName, comment);
    }
    
    /// <summary>
    /// Обработка ситуации, когда не указан ответственный за контрагента.
    /// </summary>
    /// <param name="queueItems">Все элементы очереди.</param>
    /// <param name="queueItem">Элемент очереди, по которому идёт обработка.</param>
    /// <param name="sender">Контрагент.</param>
    /// <param name="box">Ящик эл. обмена.</param>
    protected virtual void StartSimpleTaskWhenCounterpartyResponsibleNotFound(List<IMessageQueueItem> queueItems,
                                                                              IMessageQueueItem queueItem,
                                                                              Parties.ICounterparty sender,
                                                                              ExchangeCore.IBusinessUnitBox box)
    {
      if (queueItem.ResponsibleTask == null || queueItem.ResponsibleTask.Status != Workflow.SimpleTask.Status.InProcess)
      {
        // Ищем задачи по этому же КА - если они есть, то мы их переиспользуем.
        var existTask = queueItems
          .Select(q => q.ResponsibleTask)
          .Where(t => t != null && t.Status == Workflow.SimpleTask.Status.InProcess)
          .Distinct()
          .FirstOrDefault(t => t.Attachments.Contains(sender) && t.RouteSteps.Any(st => Equals(st.Performer, box.Responsible)));
        queueItem.ResponsibleTask = existTask;
        
        // Если задачи нет - создаем новую.
        if (queueItem.ResponsibleTask == null)
        {
          var task = SimpleTasks.Create(Resources.CounterpartyResponsibleNotFoundSubjectFormat(sender.Name), box.Responsible);
          task.NeedsReview = false;
          task.ActiveText += Resources.CounterpartyResponsibleNotFoundTextFormat(Hyperlinks.Get(sender));
          task.Attachments.Add(sender);
          task.Deadline = Calendar.Today.AddWorkingDays(2);
          task.Save();
          task.Start();
          queueItem.ResponsibleTask = task;
        }
        queueItem.Save();
      }
    }
    
    /// <summary>
    /// Установить статусы отказа и сверки.
    /// </summary>
    /// <param name="documentInfos">Список информаций о документах обмена.</param>
    /// <param name="isFullSet">True если комплект полный.</param>
    /// <param name="documentSetType">Тип комплекта.</param>
    private void SetStatuses(Sungero.BulkExchangeSolution.Structures.Exchange.ExchangeDocumentInfo.DocumentSet documentSet)
    {
      var documentInfos = documentSet.ExchangeDocumentInfos;
      var isFullSet = documentSet.IsFullSet;
      var documentSetType = documentSet.Type;
      var isIncoming = documentInfos.FirstOrDefault().MessageType == ExchangeDocumentInfo.MessageType.Incoming;
      
      var rejectionStatus = isIncoming && !isFullSet && documentSetType == BulkExchangeSolution.Constants.Exchange.ExchangeDocumentInfo.DocumentSetType.Waybill
        ? ExchangeDocumentInfo.RejectionStatus.Required
        : ExchangeDocumentInfo.RejectionStatus.NotRequired;

      var verificationStatus = isIncoming && isFullSet
        ? ExchangeDocumentInfo.VerificationStatus.Required
        : ExchangeDocumentInfo.VerificationStatus.NotRequired;
      
      foreach (var exhangeDoc in documentInfos)
      {
        exhangeDoc.RejectionStatus = rejectionStatus;
        exhangeDoc.VerificationStatus = verificationStatus;
        exhangeDoc.Save();
      }
    }
    
    #endregion

    #region Имитация сверки с учетной системой
    
    /// <summary>
    /// Проверка документов по учетной системе.
    /// </summary>
    /// <param name="documentSet">Комплект документов.</param>
    public void VerifyDocumentSet(Sungero.BulkExchangeSolution.Structures.Exchange.ExchangeDocumentInfo.DocumentSet documentSet)
    {
      var isIncomingDocumentSet = documentSet.ExchangeDocumentInfos.FirstOrDefault().MessageType == ExchangeDocumentInfo.MessageType.Incoming;
      if (!isIncomingDocumentSet || documentSet.Type != BulkExchangeSolution.Constants.Exchange.ExchangeDocumentInfo.DocumentSetType.Waybill)
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

      string logMessage = Resources.DocumentSetWithIDsFormat(string.Join(", ", documents.Select(d => d.Id)));
      
      var documentInfo = documentSet.ExchangeDocumentInfos.FirstOrDefault(x => Sungero.FinancialArchive.UniversalTransferDocuments.Is(x.Document) || Waybills.Is(x.Document));
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
    
    private void SetVerificationResult(DocumentSet documentSet, bool result, string reason)
    {
      foreach (var info in documentSet.ExchangeDocumentInfos)
      {
        if (result)
        {
          info.VerificationStatus = VerificationStatus.Completed;
          if (info.Document.Note == null || info.Document.Note.IndexOf(Resources.Incurred, StringComparison.InvariantCultureIgnoreCase) < 0)
          {
            var incurredNote = "*" + Resources.Incurred.ToString().ToUpper() + "*";
            if (string.IsNullOrWhiteSpace(info.Document.Note))
              info.Document.Note = incurredNote;
            else
              info.Document.Note += Environment.NewLine + incurredNote;
          }

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
    
    private bool IsDocumentVerificationCompleted(IExchangeDocumentInfo document)
    {
      return document != null && (document.ExchangeState == Sungero.Exchange.ExchangeDocumentInfo.ExchangeState.Signed || document.ExchangeState == Sungero.Exchange.ExchangeDocumentInfo.ExchangeState.Obsolete ||
                                  document.ExchangeState == Sungero.Exchange.ExchangeDocumentInfo.ExchangeState.Rejected || document.ExchangeState == Sungero.Exchange.ExchangeDocumentInfo.ExchangeState.Terminated ||
                                  (document.Document.Note != null && document.Document.Note.IndexOf(Resources.Incurred, StringComparison.InvariantCultureIgnoreCase) >= 0));
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
      if (accountDocument.ExchangeState == Docflow.OfficialDocument.ExchangeState.SignRequired && accountDocument.BuyerTitleId == null &&
          document.BusinessUnit.CEO != null)
      {
        if (!document.AccessRights.IsGrantedDirectly(DefaultAccessRightsTypes.Change, document.BusinessUnit.CEO))
        {
          document.AccessRights.Grant(document.BusinessUnit.CEO, DefaultAccessRightsTypes.Change);
          document.AccessRights.Save();
        }
        Docflow.PublicFunctions.AccountingDocumentBase.Remote.GenerateDefaultAnswer(accountDocument, document.BusinessUnit.CEO, true);
      }
    }

    #endregion

    #region Работа с комплектами
    
    /// <summary>
    /// Получить комплект документов.
    /// </summary>
    /// <param name="message">Сообщение.</param>
    /// <returns>Структура с комплектом - признак полноты и инфошки. Может быть null, если в сообщении нет никаких признаков комплекта.</returns>
    private Sungero.BulkExchangeSolution.Structures.Exchange.ExchangeDocumentInfo.DocumentSet GetDocumentSet(IMessage message)
    {
      var exchangeDocumentInfo = Sungero.BulkExchangeSolution.ExchangeDocumentInfos.GetAll().Where(e => e.ServiceMessageId == message.ServiceMessageId).FirstOrDefault();
      return exchangeDocumentInfo != null
        ? Sungero.BulkExchangeSolution.Functions.ExchangeDocumentInfo.GetDocumentSet(exchangeDocumentInfo)
        : null;
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
      var isContractStatementDocumentSet = documentSet.Type == BulkExchangeSolution.Constants.Exchange.ExchangeDocumentInfo.DocumentSetType.ContractStatement;
      
      foreach (var info in documentSet.ExchangeDocumentInfos)
      {
        var accountingDocument = Docflow.AccountingDocumentBases.As(info.Document);
        if (accountingDocument == null)
          continue;
        
        if (documentSet.Type != BulkExchangeSolution.Constants.Exchange.ExchangeDocumentInfo.DocumentSetType.ContractStatement &&
            documentSet.Type != BulkExchangeSolution.Constants.Exchange.ExchangeDocumentInfo.DocumentSetType.Waybill)
          continue;
        
        // Заполнить договор для акта.
        if (isContractStatementDocumentSet && !string.IsNullOrEmpty(info.ContractNumber))
        {
          var contract = Contracts.ContractBases.GetAll(c => Equals(accountingDocument.Counterparty, c.Counterparty) && c.RegistrationNumber == info.ContractNumber).FirstOrDefault();
          accountingDocument.LeadingDocument = contract;
          if (contract != null && contract.ResponsibleEmployee != null)
            responsible = contract.ResponsibleEmployee;
        }
        
        if (accountingDocument.IsFormalized == true)
        {
          var setNumber = isContractStatementDocumentSet ? Resources.ContractNumberFormat(info.ContractNumber) : Resources.PONumberFormat(info.PurchaseOrder);
          if (string.IsNullOrWhiteSpace(info.Document.Note))
            info.Document.Note = setNumber;
          else
            info.Document.Note += Environment.NewLine + setNumber;
        }
        if (documentSet.IsFullSet == true)
        {
          // Заполнить номенклатуру дела.
          accountingDocument.CaseFile = this.GetDefaultCaseFile(accountingDocument, info.MessageType == MessageType.Incoming, isContractStatementDocumentSet);
          
          // Выдать права главному бухгалтеру на товарные документы.
          if (!isContractStatementDocumentSet)
          {
            var chiefAccountant = Roles.GetAll(r => r.Name.Equals(Constants.Module.ChiefAccountantRoleName)).FirstOrDefault();
            accountingDocument.AccessRights.Grant(chiefAccountant, DefaultAccessRightsTypes.FullAccess);
          }
          
          // Заполнить ответственного и подразделение.
          if (responsible != null)
            this.SetDocumentResponsible(accountingDocument, responsible);
        }
        
        // Сохранить документ, если были изменения.
        if (accountingDocument.State.IsChanged)
          accountingDocument.Save();
      }
      if (documentSet.IsFullSet == true && documentSet.ExchangeDocumentInfos.Count == 2)
        this.AddRelationsForDocumentSet(documentSet);
    }

    /// <summary>
    /// Создать связь документов комплекта.
    /// </summary>
    /// <param name="documentSet">Комплект.</param>
    private void AddRelationsForDocumentSet(Sungero.BulkExchangeSolution.Structures.Exchange.ExchangeDocumentInfo.DocumentSet documentSet)
    {
      var exchangeDocuments = documentSet.ExchangeDocumentInfos.Select(e => e.Document).ToList();
      this.AddRelationsForDocuments(exchangeDocuments);
    }
    
    /// <summary>
    /// Создать связь документов комплекта.
    /// </summary>
    /// <param name="documents">Документы комплекта.</param>
    [Public, Remote]
    public void AddRelationsForDocuments(List<IOfficialDocument> documents)
    {
      var firstDocument = OfficialDocuments.As(documents[0]);
      var secondDocument = OfficialDocuments.As(documents[1]);
      
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
    
    private void SendDocumentProcessingTask(DocumentSet documentSet, bool verificationResult)
    {
      var documentInfo =
        documentSet.ExchangeDocumentInfos.FirstOrDefault(x =>
                                                         Sungero.FinancialArchive.UniversalTransferDocuments.Is(x.Document) || Waybills.Is(x.Document));
      var createTime = documentSet.ExchangeDocumentInfos.Select(x => x.Document.Created).Max();
      
      if (documentInfo.VerificationTask == null &&
          Calendar.Now - createTime > TimeSpan.FromHours(Constants.Module.DocumentVerificationDeadlineInHours) &&
          !verificationResult)
      {
        var client =
          ExchangeCore.PublicFunctions.BusinessUnitBox.GetPublicClient(documentInfo.RootBox) as
          NpoComputer.DCX.ClientApi.Client;
        var message = client.GetMessage(documentInfo.ServiceMessageId);
        var isIncoming = message.Sender.Organization.OrganizationId != documentInfo.RootBox.OrganizationId;

        var taskText = Environment.NewLine + Sungero.BulkExchangeSolution.Module.Exchange.Resources.VerificationFailedTaskText +
          documentInfo.VerificationFailReason;
        var processingTask = this.CreateExchangeTask(message,
                                                     documentSet.ExchangeDocumentInfos.Select(x => Sungero.Exchange.ExchangeDocumentInfos.As(x)).ToList(), documentInfo.Counterparty, isIncoming, documentInfo.RootBox, taskText);
        processingTask.Start();
        documentInfo.VerificationTask = processingTask;
        documentInfo.Save();
      }
      
      // Если задачу уже прекратили или завершили, а сверка повторяется - сверка неактуальна.
      if (documentInfo.VerificationTask != null && documentInfo.VerificationTask.Status == Workflow.Task.Status.Completed)
      {
        foreach (var info in documentSet.ExchangeDocumentInfos.Where(i => i.VerificationStatus == VerificationStatus.Required))
        {
          info.VerificationStatus = VerificationStatus.NotRequired;
          info.Save();
        }
      }
    }
    
    /// <summary>
    /// Отправить комплект документов по услугам на согласование по регламенту.
    /// </summary>
    /// <param name="documentSet">Комплект.</param>
    private void SendContractStatementForApproval(Sungero.BulkExchangeSolution.Structures.Exchange.ExchangeDocumentInfo.DocumentSet documentSet)
    {
      if (documentSet.Type != BulkExchangeSolution.Constants.Exchange.ExchangeDocumentInfo.DocumentSetType.ContractStatement)
        return;
      
      var contractStatement = documentSet.ExchangeDocumentInfos.Select(i => Docflow.AccountingDocumentBases.As(i.Document)).First(d => FinancialArchive.ContractStatements.Is(d) ||
                                                                                                                                  FinancialArchive.UniversalTransferDocuments.Is(d));
      var task = Docflow.PublicFunctions.Module.Remote.CreateApprovalTask(contractStatement);
      if (contractStatement.ResponsibleEmployee != null)
        task.Author = contractStatement.ResponsibleEmployee;
      
      task.Start();
    }
    
    /// <summary>
    /// Получить дополнительные элементы из xml формализованного документа.
    /// </summary>
    /// <param name="xml">Содержимое документа.</param>
    /// <returns>Дополнительные элементы.</returns>
    [Public]
    public virtual List<System.Xml.Linq.XElement> GetAdditionalProperties(byte[] xml)
    {
      System.Xml.Linq.XDocument xdoc;
      try
      {
        xdoc = System.Xml.Linq.XDocument.Load(new System.IO.MemoryStream(xml));
      }
      catch (System.Xml.XmlException)
      {
        return new List<System.Xml.Linq.XElement>();
      }
      
      RemoveNamespaces(xdoc);
      var additionalProperties = xdoc.Descendants("ТекстИнф").ToList();
      
      // В ДПРР это может быть другой xml элемент.
      var contractStatementAdditionalProperties = xdoc.Descendants("ИнфПолФХЖ2").ToList();
      if (contractStatementAdditionalProperties.Any())
        additionalProperties.AddRange(contractStatementAdditionalProperties);
      
      // В ДПТ это может быть другой xml элемент.
      var waybillAdditionalProperties = xdoc.Descendants("ИнфПолФХЖ3").ToList();
      if (waybillAdditionalProperties.Any())
        additionalProperties.AddRange(waybillAdditionalProperties);
      
      return additionalProperties;
    }
    
    /// <summary>
    /// Получить номер заказа из элементов xml формализованного документа.
    /// </summary>
    /// <param name="xmlElements">Xml элементы формализованного документа.</param>
    /// <returns>Номер заказа.</returns>
    [Public]
    public virtual string GetPurchaseOrderNumber(List<System.Xml.Linq.XElement> xmlElements)
    {
      var purchaseOrderElement = xmlElements.FirstOrDefault(i => (string)i.Attribute("Идентиф") == Constants.Module.PurchaseOrder);
      if (purchaseOrderElement != null)
        return purchaseOrderElement.Attribute("Значен").Value;
      
      return string.Empty;
    }
    
    /// <summary>
    /// Получить номер договора из элементов xml формализованного документа.
    /// </summary>
    /// <param name="xmlElements">Xml элементы формализованного документа.</param>
    /// <returns>Номер договора.</returns>
    [Public]
    public virtual string GetContractNumber(List<System.Xml.Linq.XElement> xmlElements)
    {
      var contractStatementElement = xmlElements.FirstOrDefault(i => (string)i.Attribute("Идентиф") == Constants.Module.ContractNumber);
      if (contractStatementElement != null)
        return contractStatementElement.Attribute("Значен").Value;
      
      return string.Empty;
    }
    
    #endregion

    #region Импорт товарных и нетоварных комплектов

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
    
    /// <summary>
    /// Заполнить ответственного, подразделение ответственного и выдать ему права на документ.
    /// </summary>
    /// <param name="accountingDocument">Документ.</param>
    /// <param name="responsible">Ответственный.</param>
    [Public]
    public void SetDocumentResponsible(IAccountingDocumentBase accountingDocument, Sungero.Company.IEmployee responsible)
    {
      if (responsible == null)
        return;
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
      this.GrantAccessRightsForResponsible(accountingDocument, responsible);
    }
    
    protected virtual void GrantAccessRightsForResponsible(IOfficialDocument document, Company.IEmployee responsible)
    {
      if (!document.AccessRights.IsGranted(DefaultAccessRightsTypes.FullAccess, responsible))
      {
        document.AccessRights.Grant(responsible, DefaultAccessRightsTypes.FullAccess);
        document.AccessRights.Save();
      }
    }
    
    [Public]
    /// <summary>
    /// Получить номенклатуру дела для документа.
    /// </summary>
    /// <param name="document">Документ.</param>
    /// <param name="isIncoming">Признак входящего документа.</param>
    /// <param name="isContractStatement">Признак нетоварного потока.</param>
    /// <returns>Номенклатуру дела.</returns>
    public virtual ICaseFile GetDefaultCaseFile(IOfficialDocument document, bool isIncoming, bool isContractStatement)
    {
      var caseFileName = string.Empty;
      
      if (Sungero.FinancialArchive.IncomingTaxInvoices.Is(document))
        caseFileName = "Счета-фактуры полученные";
      if (Sungero.FinancialArchive.OutgoingTaxInvoices.Is(document))
        caseFileName = "Счета-фактуры выставленные";
      if (Sungero.FinancialArchive.Waybills.Is(document))
        caseFileName = isIncoming ? "Товарные накладные полученные" : "Товарные накладные выставленные";
      if (Sungero.FinancialArchive.ContractStatements.Is(document))
        caseFileName = isIncoming ? "Акты выполненных работ полученные" : "Акты выполненных работ выставленные";
      if (Sungero.FinancialArchive.UniversalTransferDocuments.Is(document))
      {
        if (isContractStatement)
          caseFileName = isIncoming ? "Акты выполненных работ полученные" : "Акты выполненных работ выставленные";
        else
          caseFileName = isIncoming ? "Товарные накладные полученные" : "Товарные накладные выставленные";
      }
      if (!string.IsNullOrEmpty(caseFileName))
        return Docflow.CaseFiles.GetAll(c => c.Title == caseFileName).FirstOrDefault();
      return null;
    }
    #endregion
    
  }
}