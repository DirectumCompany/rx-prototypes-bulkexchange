using System;
using System.Collections.Generic;
using System.Linq;
using Sungero.Core;
using Sungero.CoreEntities;

namespace Sungero.BulkExchangeSolution.Client
{
  public class ModuleFunctions
  {
    [Public]
    public virtual void SignCheckedDocuments()
    {
      var checkedSets = Sungero.BulkExchangeSolution.Module.Exchange.Functions.Module.Remote.GetCheckedSets();
      foreach (var checkedSet in checkedSets)
      {
        Logger.DebugFormat("Sign document set with infos {0}", string.Join(", ", checkedSet.ExchangeDocumentInfos.Select(i => i.Id)));
        foreach (var info in checkedSet.ExchangeDocumentInfos)
        {
          if (info.NeedSign == true)
          {
            var certificate = BusinessUnitBoxes.As(info.RootBox).SignDocumentCertificate;
            if (Docflow.PublicFunctions.OfficialDocument.ApproveWithAddenda(info.Document, null, certificate, "Автоподпись", null, false, null))
              BulkExchangeSolution.Functions.ExchangeDocumentInfo.Remote.ChangeSignStatus(info);
          }
        }
      }
    }
    
  }
}