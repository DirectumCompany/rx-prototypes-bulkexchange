using System;
using System.Collections.Generic;
using System.Linq;
using Sungero.Core;
using Sungero.CoreEntities;

namespace Sungero.BulkExchangeSolution.Client
{
  public class ModuleFunctions
  {
    public virtual void SignCheckedDocuments()
    {
      var checkedSets = Sungero.BulkExchangeSolution.Module.Exchange.Functions.Module.Remote.GetCheckedSets();
      var messageIds = checkedSets.Select(x => x.ServiceMessageId).Distinct().ToList();
      foreach (var messageId in messageIds)
      {
        var infos = checkedSets.Where(x => x.ServiceMessageId == messageId);
        Logger.DebugFormat("Sign document set with infos {0}", string.Join(", ", infos.Select(i => i.Id)));
        foreach (var info in infos)
        {
          if (info.NeedSign == true)
          {
            var certificate = BusinessUnitBoxes.As(info.RootBox).SignDocumentCertificate;
            if (Docflow.PublicFunctions.OfficialDocument.ApproveWithAddenda(info.Document, null, certificate, "Автоподпись", null, false, null))
            {
              info.SignStatus = Sungero.BulkExchangeSolution.ExchangeDocumentInfo.SignStatus.Signed;
              info.Save();
            }
          }
        }
      }
    }
    
  }
}