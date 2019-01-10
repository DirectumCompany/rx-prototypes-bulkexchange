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
      if (FinancialArchive.UniversalTransferDocuments.Is(document) || FinancialArchive.Waybills.Is(document))
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
    
    [Remote(IsPure = true)]
    public virtual IQueryable<IExchangeDocumentInfo> GetCheckedDocuments()
    {
      // все накладные с РО, прошедшие сверку
      var boxes = Sungero.ExchangeCore.PublicFunctions.BusinessUnitBox.Remote.GetConnectedBoxes().Where(r => BusinessUnitBoxes.As(r).SignDocumentCertificate != null).ToList();
      
      return ExchangeDocumentInfos.GetAll()
        .Where(x => boxes.Contains(x.RootBox) && x.CheckStatus == ExchangeDocumentInfo.CheckStatus.Completed && x.PurchaseOrder != null && x.ExchangeState == ExchangeDocumentInfo.ExchangeState.SignRequired &&
               x.SignStatus == ExchangeDocumentInfo.SignStatus.Required);
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
    public override void AddRelations(Docflow.IOfficialDocument document, Sungero.Exchange.IExchangeDocumentInfo relatedExchangeDocumentInfo)
    {
      var relatedDocument = relatedExchangeDocumentInfo.Document;
      var docInfos = Sungero.BulkExchangeSolution.ExchangeDocumentInfos.GetAll()
        .Where(i => Equals(i.ServiceMessageId, relatedExchangeDocumentInfo.ServiceMessageId));
      
      var dop = Docflow.AccountingDocumentBase.FormalizedFunction.Dop;
      var schf = Docflow.AccountingDocumentBase.FormalizedFunction.Schf;
      
      var documentFormalizedFunction = Docflow.AccountingDocumentBases.Is(document) ?
        Docflow.AccountingDocumentBases.As(document).FormalizedFunction :
        null;
      
      var relatedDocumentFormalizedFunction = Docflow.AccountingDocumentBases.Is(relatedDocument) ?
        Docflow.AccountingDocumentBases.As(relatedDocument).FormalizedFunction :
        null;
      
      if (docInfos.Count() < 3 && documentFormalizedFunction == dop && relatedDocumentFormalizedFunction == schf)
        document.Relations.AddOrUpdate(Sungero.Exchange.Constants.Module.AddendumRelationName, null, relatedDocument);
      else if(docInfos.Count() < 3 && documentFormalizedFunction == schf && relatedDocumentFormalizedFunction == dop)
        document.Relations.AddFromOrUpdate(Sungero.Exchange.Constants.Module.AddendumRelationName, null, relatedDocument);
      else
        document.Relations.AddFromOrUpdate(Sungero.Exchange.Constants.Module.SimpleRelationRelationName, null, relatedDocument);
    }
  }
}