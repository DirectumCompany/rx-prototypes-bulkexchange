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
      var checkedSets = Module.Exchange.Functions.Module.Remote.GetCheckedSets().Where(c => BusinessUnitBoxes.As(c.RootBox).SignDocumentCertificate != null &&
                                                                                                                    Equals(BusinessUnitBoxes.As(c.RootBox).SignDocumentCertificate.Owner, Users.Current));
      var messageIds = checkedSets.Select(x => x.ServiceMessageId).Distinct().ToList();
      foreach (var messageId in messageIds)
      {
        var infos = checkedSets.Where(x => x.ServiceMessageId == messageId);
        Logger.DebugFormat("Start sign document set with document ids {0}.", string.Join(", ", infos.Select(i => i.Document.Id)));
        foreach (var info in infos)
        {
          if (info.NeedSign == true)
          {
            var certificate = BusinessUnitBoxes.As(info.RootBox).SignDocumentCertificate;
            try
            {
              if (Docflow.PublicFunctions.OfficialDocument.ApproveWithAddenda(info.Document, null, certificate, "Автоподпись", null, false, null))
              {
                Logger.DebugFormat("Sign document set with document ids {0} successfully.", string.Join(", ", info.Document.Id));
                info.SignStatus = Sungero.BulkExchangeSolution.ExchangeDocumentInfo.SignStatus.Signed;
                info.Save();
              }
            }
            catch (Exception ex)
            {
              Logger.Error(Sungero.BulkExchangeSolution.Resources.CannotSignDocument, ex);
            }
          }
        }
      }
    }
    
  }
}