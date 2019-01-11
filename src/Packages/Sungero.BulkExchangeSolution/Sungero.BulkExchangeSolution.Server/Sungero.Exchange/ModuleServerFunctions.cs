using System;
using System.Collections.Generic;
using System.Linq;
using Sungero.BulkExchangeSolution.ExchangeDocumentInfo;
using Sungero.Core;
using Sungero.CoreEntities;

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
  }
}