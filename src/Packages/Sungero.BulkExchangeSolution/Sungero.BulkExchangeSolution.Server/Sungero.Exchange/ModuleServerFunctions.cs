using System;
using System.Collections.Generic;
using System.Linq;
using Sungero.BulkExchangeSolution.ExchangeDocumentInfo;
using Sungero.Commons;
using Sungero.Core;
using Sungero.CoreEntities;
using Sungero.Docflow;
using Sungero.ExchangeCore;
using Sungero.Parties;
using Sungero.Workflow;

namespace Sungero.BulkExchangeSolution.Module.Exchange.Server
{
  partial class ModuleFunctions
  {
    protected override Sungero.Docflow.IOfficialDocument GetOrCreateNewExchangeDocument(Sungero.ExchangeCore.IBoxBase box, object documentUntyped, Sungero.Parties.ICounterparty sender, string serviceCounterpartyId, DateTime messageDate, bool isIncoming)
    {
      var document = base.GetOrCreateNewExchangeDocument(box, documentUntyped, sender, serviceCounterpartyId, messageDate, isIncoming);
      if (FinancialArchive.UniversalTransferDocuments.Is(document) || FinancialArchive.IncomingTaxInvoices.Is(document) || FinancialArchive.Waybills.Is(document))
      {
        var serviceDocument = documentUntyped as NpoComputer.DCX.Common.IDocument;
        var xdoc = System.Xml.Linq.XDocument.Load(new System.IO.MemoryStream(serviceDocument.Content));
        RemoveNamespaces(xdoc);
        var additionalProperties = xdoc.Descendants("ТекстИнф");
        if (additionalProperties.Any())
        {
          var purchaseOrderElement = additionalProperties.FirstOrDefault(i => (string)i.Attribute("Идентиф") == "номер_заказа");
          if (purchaseOrderElement != null)
          {
            var exchangeDocumentInfo = ExchangeDocumentInfos.As(Sungero.Exchange.PublicFunctions.ExchangeDocumentInfo.GetExDocumentInfoByExternalId(box, serviceDocument.ServiceEntityId));
            exchangeDocumentInfo.PurchaseOrder = purchaseOrderElement.Attribute("Значен").Value;
            exchangeDocumentInfo.CheckStatus = CheckStatus.Required;
            exchangeDocumentInfo.Save();
          }
          var caseFile = Docflow.CaseFiles.GetAll(c => c.Status == Docflow.CaseFile.Status.Active).FirstOrDefault();
          document.CaseFile = caseFile;
          document.Save();
        }
      }
      return document;
    }
    
    [Remote]
    public virtual List<Sungero.BulkExchangeSolution.IExchangeDocumentInfo> GetCheckedSets()
    {
      // все накладные с РО, прошедшие сверку
      var boxes = Sungero.ExchangeCore.PublicFunctions.BusinessUnitBox.Remote.GetConnectedBoxes().Where(c => c.HasExchangeServiceCertificates == true &&
                                                                                                        c.ExchangeServiceCertificates.Any(x => Equals(x.Certificate.Owner, Users.Current) &&
                                                                                                                                          x.Certificate.Enabled == true)).ToList();
      var infos = ExchangeDocumentInfos.GetAll()
        .Where(x => boxes.Contains(x.RootBox) && x.ExchangeState == ExchangeDocumentInfo.ExchangeState.SignRequired &&
               (x.SignStatus == null || x.SignStatus != SignStatus.Signed) && x.CheckStatus == ExchangeDocumentInfo.CheckStatus.Completed);
      
      var result = new List<Sungero.BulkExchangeSolution.IExchangeDocumentInfo>();
      foreach (var documentSet in Sungero.BulkExchangeSolution.Functions.ExchangeDocumentInfo.GetDocumentSets(infos.ToList()).Where(x => x.IsFullSet).ToList())
        result.AddRange(documentSet.ExchangeDocumentInfos);
      
      return result;
    }
    
    public virtual List<Structures.Exchange.ExchangeDocumentInfo.DocumentSet> GetSignedAndNotSendedDocumentSets()
    {
      var boxes = Sungero.ExchangeCore.PublicFunctions.BusinessUnitBox.Remote.GetConnectedBoxes().ToList();
      
      var infos = ExchangeDocumentInfos.GetAll()
        .Where(x => boxes.Contains(x.RootBox)
               && x.CheckStatus == ExchangeDocumentInfo.CheckStatus.Completed
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

    public void CheckDocumentSet(Structures.Exchange.ExchangeDocumentInfo.DocumentSet documentSet)
    {
      var totalAmount = Sungero.Docflow.AccountingDocumentBases.As(documentSet.ExchangeDocumentInfos.FirstOrDefault().Document).TotalAmount;
      var result = true;
      var reason = string.Empty;
      var documents = new List<IOfficialDocument>();
      
      foreach (var documentInfo in documentSet.ExchangeDocumentInfos)
      {
        documents.Add(documentInfo.Document);
        var document = AccountingDocumentBases.As(documentInfo.Document);
        if (document.TotalAmount != totalAmount)
        {
          result = false;
          reason = Resources.DocumentsTotalAmountError;
        }

        var rubCurrency = Currencies.GetAll().FirstOrDefault(x =>
                                                             string.Equals(x.AlphaCode, Sungero.BulkExchangeSolution.Module.Exchange.Resources.RubAlphaCode,
                                                                           StringComparison.InvariantCultureIgnoreCase));
        
        if (!Equals(document.Currency, rubCurrency) || document.TotalAmount >= 100000)
        {
          result = false;
          reason = Resources.TotalAmountIsTooBig;
        }
      }

      var logMessage = Sungero.BulkExchangeSolution.Module.Exchange.Resources.DocumentSetWithIDs.ToString();
      for (int i = 0; i < documents.Count; i++)
        logMessage += i == 0 ? documents[i].Id.ToString() : ", " + documents[i].Id;
      
      if (!result)
      {
        IExchangeDocumentInfo documentInfo = documentSet.ExchangeDocumentInfos.FirstOrDefault(x => Sungero.FinancialArchive.UniversalTransferDocuments.Is(x.Document));
        var task = documentInfo.CheckTask;
        if ((task == null || task != null && task.Status == Workflow.Task.Status.Completed) &&
            this.IsCheckDocumentCompleted(documentInfo))
          result = true;
        var createTime = documentSet.ExchangeDocumentInfos.Select(x => x.Document.Created).Max();
        if ((task == null || task.Status != Workflow.Task.Status.InProcess) && Calendar.Now - createTime > TimeSpan.FromHours(1) && !result)
        {
          var client = ExchangeCore.PublicFunctions.BusinessUnitBox.GetPublicClient(documentInfo.RootBox) as NpoComputer.DCX.ClientApi.Client;
          var message = client.GetMessage(documentInfo.ServiceMessageId);
          var isIncoming = message.Sender.Organization.OrganizationId != documentInfo.RootBox.OrganizationId;
          var needSign = documentSet.ExchangeDocumentInfos.Select(i => i.Document).Where(d => FinancialArchive.UniversalTransferDocuments.Is(d)).ToList();
          var notNeedSign = documentSet.ExchangeDocumentInfos.Select(i => i.Document).Where(d => FinancialArchive.IncomingTaxInvoices.Is(d)).ToList();

          var taskText = Environment.NewLine + Sungero.BulkExchangeSolution.Module.Exchange.Resources.CheckFailedTaskText + documentInfo.CheckFailReason;
          var processingTask = this.CreateExchangeTask(documentInfo.RootBox, message, documentInfo.Counterparty, isIncoming,
                                                       needSign, new List<IOfficialDocument>(), new List<NpoComputer.DCX.Common.IDocument>(),
                                                       notNeedSign, taskText);
          processingTask.Start();
          documentInfo.CheckTask = processingTask;
          documentInfo.Save();
        }
      }
      
      logMessage += result ? Sungero.BulkExchangeSolution.Module.Exchange.Resources.CheckSuccess : Sungero.BulkExchangeSolution.Module.Exchange.Resources.CheckFail + reason;
      Logger.Debug(logMessage);
      
      foreach (var documentInfo in documentSet.ExchangeDocumentInfos)
      {
        if (result)
        {
          documentInfo.CheckStatus = CheckStatus.Completed;
          documentInfo.CheckFailReason = null;
        }
        else
        {
          documentInfo.CheckFailReason = reason;
          documentInfo.CheckStatus = CheckStatus.Required;
        }
        documentInfo.Save();
      }
    }
    
    public override void StartExchangeTask(Sungero.ExchangeCore.IBoxBase box,
                                           object messageUntyped,
                                           Parties.ICounterparty sender,
                                           bool isIncoming,
                                           List<Sungero.Docflow.IOfficialDocument> needSign,
                                           List<Sungero.Docflow.IOfficialDocument> signed,
                                           object rejectedUntyped,
                                           List<Sungero.Docflow.IOfficialDocument> dontNeedSign,
                                           string exchangeTaskActiveTextBoundedDocuments)
    {
      var message = messageUntyped as NpoComputer.DCX.Common.IMessage;
      var exchangeDocumentInfos = Sungero.BulkExchangeSolution.ExchangeDocumentInfos.GetAll().Where(e => e.ServiceMessageId == message.ServiceMessageId).ToList();
      if (exchangeDocumentInfos.Any(i => i.CheckStatus == CheckStatus.Required))
        return;
      
      base.StartExchangeTask(box, messageUntyped, sender, isIncoming, needSign, signed, rejectedUntyped, dontNeedSign, exchangeTaskActiveTextBoundedDocuments);
    }

    public override Sungero.Exchange.IExchangeDocumentProcessingTask CreateExchangeTask(Sungero.ExchangeCore.IBoxBase box,
                                                                                        object messageUntyped,
                                                                                        Parties.ICounterparty sender,
                                                                                        bool isIncoming,
                                                                                        List<Sungero.Docflow.IOfficialDocument> needSign,
                                                                                        List<Sungero.Docflow.IOfficialDocument> signed,
                                                                                        object rejectedUntyped,
                                                                                        List<Sungero.Docflow.IOfficialDocument> dontNeedSign,
                                                                                        string exchangeTaskActiveTextBoundedDocuments)
    {
      var task = base.CreateExchangeTask(box, messageUntyped, sender, isIncoming, needSign, signed, rejectedUntyped, dontNeedSign, exchangeTaskActiveTextBoundedDocuments);
      return task;
    }
    
    private bool IsCheckDocumentCompleted(IExchangeDocumentInfo document)
    {
      return document != null && (document.ExchangeState == Sungero.Exchange.ExchangeDocumentInfo.ExchangeState.Signed || document.ExchangeState == Sungero.Exchange.ExchangeDocumentInfo.ExchangeState.Obsolete ||
                                  document.ExchangeState == Sungero.Exchange.ExchangeDocumentInfo.ExchangeState.Rejected || document.ExchangeState == Sungero.Exchange.ExchangeDocumentInfo.ExchangeState.Terminated ||
                                  string.Equals(document.Document.Note.Trim(), "проведено", StringComparison.InvariantCultureIgnoreCase));
    }
    
    /// <summary>
    /// Обработать документы, созданные из сообщения.
    /// </summary>
    /// <param name="box">Абонентский ящик.</param>
    /// <param name="messageUntyped">Сообщение.</param>
    /// <param name="sender">Отправитель.</param>
    /// <param name="queueItem">Элемент очереди.</param>
    /// <param name="isIncoming">True - от контрагента, false - наше.</param>
    /// <param name="needSign">Коллекция документов, требующих подписания.</param>
    /// <param name="dontNeedSign">Коллекция документов, требующих подписания.</param>
    /// <param name="signed">Коллекция уже подписанных документов.</param>
    /// <param name="processingDocuments">Обрабатываемые документы.</param>
    /// <param name="rejected">Коллекция документов, по которым отказано.</param>
    protected override void ProcessMessageDocuments(ExchangeCore.IBoxBase box, object messageUntyped, Parties.ICounterparty sender,
                                                    ExchangeCore.IMessageQueueItem queueItem, bool isIncoming, List<IOfficialDocument> needSign, List<IOfficialDocument> dontNeedSign, List<IOfficialDocument> signed,
                                                    object untypedProcessingDocuments, object untypedRejected)
    {
      var message = messageUntyped as NpoComputer.DCX.Common.IMessage;
      var exchangeDocumentInfos = Sungero.BulkExchangeSolution.ExchangeDocumentInfos.GetAll().Where(e => e.ServiceMessageId == message.ServiceMessageId).ToList();
      var documentSets = Sungero.BulkExchangeSolution.Functions.ExchangeDocumentInfo.GetDocumentSets(exchangeDocumentInfos);
      foreach (var documentSet in documentSets)
      {
        Functions.Module.CheckDocumentSet(documentSet);
      }
      base.ProcessMessageDocuments(box, messageUntyped, sender, queueItem, isIncoming, needSign, dontNeedSign, signed, untypedProcessingDocuments, untypedRejected);
      this.AddRelationsForDocumentSet(messageUntyped);
    }
    
    /// <summary>
    /// Создать связь документов комплекта.
    /// </summary>
    /// <param name="messageUntyped">Сообщение.</param>
    private void AddRelationsForDocumentSet(object messageUntyped)
    {
      var message = messageUntyped as NpoComputer.DCX.Common.IMessage;
      var exchangeDocumentInfo = Sungero.BulkExchangeSolution.ExchangeDocumentInfos.GetAll().Where(e => e.ServiceMessageId == message.ServiceMessageId).FirstOrDefault();
      var documentSet = exchangeDocumentInfo != null
        ? Sungero.BulkExchangeSolution.Functions.ExchangeDocumentInfo.GetDocumentSet(exchangeDocumentInfo)
        : null;
      
      if (documentSet != null && documentSet.IsFullSet && documentSet.ExchangeDocumentInfos.Count == 2)
      {
        var exchangeDocuments = documentSet.ExchangeDocumentInfos.Select(e => e.Document).ToList();
        var firstDocument = AccountingDocumentBases.As(exchangeDocuments[0]);
        var secondDocument = AccountingDocumentBases.As(exchangeDocuments[1]);
        if (firstDocument.FormalizedFunction == Docflow.AccountingDocumentBase.FormalizedFunction.Dop)
        {
          firstDocument.Relations.AddOrUpdate(Sungero.Exchange.Constants.Module.AddendumRelationName, null, secondDocument);
          firstDocument.Save();
        }
        else
        {
          secondDocument.Relations.AddOrUpdate(Sungero.Exchange.Constants.Module.AddendumRelationName, null, firstDocument);
          secondDocument.Save();
        }
      }
    }
  }
}