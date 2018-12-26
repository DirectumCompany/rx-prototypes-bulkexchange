using System;
using System.Collections.Generic;
using System.Linq;
using Sungero.Core;
using Sungero.CoreEntities;

namespace Sungero.BulkExchangeSolution.Module.Exchange.Client
{
  partial class ModuleFunctions
  {
    public virtual void SignCheckedDocuments()
    {
      var infos = Functions.Module.Remote.GetCheckedDocuments();
      foreach (var info in infos)
      {
        var certificate = BusinessUnitBoxes.As(info.RootBox).SignDocumentCertificate;
        if (Docflow.PublicFunctions.OfficialDocument.ApproveWithAddenda(info.Document, null, certificate, "Автоподпись", null, false, null))
        {
          info.SignStatus = ExchangeDocumentInfo.SignStatus.Signed;
          info.Save();
        }
      }
    }
  }
}