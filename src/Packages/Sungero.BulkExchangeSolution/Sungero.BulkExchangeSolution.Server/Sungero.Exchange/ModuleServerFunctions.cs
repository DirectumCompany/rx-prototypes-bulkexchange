using System;
using System.Collections.Generic;
using System.Linq;
using Sungero.BulkExchangeSolution.ExchangeDocumentInfo;
using Sungero.Commons;
using Sungero.Core;
using Sungero.CoreEntities;
using Sungero.Docflow;
using Sungero.Workflow;

namespace Sungero.BulkExchangeSolution.Module.Exchange.Server
{
  partial class ModuleFunctions
  {
    protected override Sungero.Docflow.IOfficialDocument GetOrCreateNewExchangeDocument(Sungero.ExchangeCore.IBoxBase box, object clientUntyped, object documentUntyped, Sungero.Parties.ICounterparty sender, string serviceCounterpartyId, DateTime messageDate, bool isIncoming)
    {
      var document = base.GetOrCreateNewExchangeDocument(box, clientUntyped, documentUntyped, sender, serviceCounterpartyId, messageDate, isIncoming);
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
      var boxes = Sungero.ExchangeCore.PublicFunctions.BusinessUnitBox.Remote.GetConnectedBoxes().Where(r => BusinessUnitBoxes.As(r).SignDocumentCertificate != null).ToList();
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
      var boxes = Sungero.ExchangeCore.PublicFunctions.BusinessUnitBox.Remote.GetConnectedBoxes().Where(r => BusinessUnitBoxes.As(r).SignDocumentCertificate != null).ToList();
      
      var infos = ExchangeDocumentInfos.GetAll()
        .Where(x => boxes.Contains(x.RootBox)
               && x.CheckStatus == ExchangeDocumentInfo.CheckStatus.Completed
               && x.ExchangeState == ExchangeDocumentInfo.ExchangeState.SignRequired
               && x.SignStatus == ExchangeDocumentInfo.SignStatus.Signed
               && x.ReceiverSignId == null);
      
      return BulkExchangeSolution.Functions.ExchangeDocumentInfo.GetDocumentSets(infos.ToList()).Where(s => s.IsFullSet).ToList();
    }
    
    /// <summary>
    /// Связать документы.
    /// </summary>
    /// <param name="document">Документ.</param>
    /// <param name="relatedExchangeDocumentInfo">Информация о связываемом документе обмена.</param>
    public override void AddRelations(Docflow.IOfficialDocument document, Sungero.Exchange.IExchangeDocumentInfo relatedExchangeDocInfo)
    {
      var exchangeDocumentInfo = Sungero.BulkExchangeSolution.ExchangeDocumentInfos.GetAll().Where(d => Equals(d.Document, document)).FirstOrDefault();
      var relatedExchangeDocumentInfo = Sungero.BulkExchangeSolution.ExchangeDocumentInfos.As(relatedExchangeDocInfo);
      
      if (exchangeDocumentInfo == null || relatedExchangeDocumentInfo == null)
        return;
      
      var relatedDocument = relatedExchangeDocumentInfo.Document;
      var documentSet = Sungero.BulkExchangeSolution.Functions.ExchangeDocumentInfo.GetDocumentSet(relatedExchangeDocumentInfo);
      
      if (documentSet != null && documentSet.IsFullSet)
      {
        var documentFormalizedFunction = Docflow.AccountingDocumentBases.Is(document) ?
          Docflow.AccountingDocumentBases.As(document).FormalizedFunction :
          null;
   
        if (documentFormalizedFunction == Docflow.AccountingDocumentBase.FormalizedFunction.Dop)
          document.Relations.AddOrUpdate(Sungero.Exchange.Constants.Module.AddendumRelationName, null, relatedDocument);
        
        return;
      }
      
      document.Relations.AddFromOrUpdate(Sungero.Exchange.Constants.Module.SimpleRelationRelationName, null, relatedDocument);
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
        logMessage += i == 0 ? documents[i].Id.ToString() : documents[i].Id + ", ";
      
      if (!result)
      {
        var documentInfo = documentSet.ExchangeDocumentInfos.FirstOrDefault(x => Sungero.FinancialArchive.UniversalTransferDocuments.Is(x.Document));
        var task = documentInfo.CheckTask;
        if ((task == null || task != null && task.Status == Workflow.Task.Status.Completed) &&
            this.IsCheckDocumentCompleted(OfficialDocuments.As(documentInfo.Document)))
          result = true;
        //TODO: Create task by documentSet with override function
        if (task == null || task.Status != Workflow.Task.Status.InProcess)
        {
          var createTime = documentSet.ExchangeDocumentInfos.Select(x => x.Document.Created).Max();
//          var processingTask = this.CreateExchangeTask(documentInfo.RootBox, documentInfo.Counterparty, documentInfo.);
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
    
    private bool IsCheckDocumentCompleted(IOfficialDocument document)
    {
      return document != null && (document.ExchangeState == Docflow.OfficialDocument.ExchangeState.Signed || document.ExchangeState == Docflow.OfficialDocument.ExchangeState.Obsolete ||
                                  document.ExchangeState == Docflow.OfficialDocument.ExchangeState.Rejected || document.ExchangeState == Docflow.OfficialDocument.ExchangeState.Terminated ||
                                  string.Equals(document.Note.Trim(), "проведено", StringComparison.InvariantCultureIgnoreCase));
    }
    
  }
}