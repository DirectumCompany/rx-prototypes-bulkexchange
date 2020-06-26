using System;
using System.Collections.Generic;
using System.Linq;
using Sungero.Core;
using Sungero.CoreEntities;
using Sungero.SmartProcessing.Structures.Module;

namespace Sungero.BulkExchangeSolution.Module.SmartProcessing.Server
{
  partial class ModuleFunctions
  {
    
    public override void FillAccountingDocumentParties(Docflow.IAccountingDocumentBase accountingDocument,
                                                      IDocumentInfo documentInfo,
                                                      IRecognizedDocumentParties recognizedDocumentParties)
    {
      if (BulkExchangeSolution.Blobs.As(documentInfo.ArioDocument.OriginalBlob).Document == null)
        base.FillAccountingDocumentParties(accountingDocument, documentInfo, recognizedDocumentParties);
      
    }

  }
}