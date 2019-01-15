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
      var checkedSets = Module.Exchange.Functions.Module.Remote.GetCheckedSets().Where(c => c.RootBox.HasExchangeServiceCertificates == true &&
                                                                                       c.RootBox.ExchangeServiceCertificates.Any(x => Equals(x.Certificate.Owner, Users.Current) && x.Certificate.Enabled == true));
      var messageIds = checkedSets.Select(x => x.ServiceMessageId).Distinct().ToList();
      foreach (var messageId in messageIds)
      {
        var infos = checkedSets.Where(x => x.ServiceMessageId == messageId);
        Logger.DebugFormat("Start sign document set with document ids {0}.", string.Join(", ", infos.Select(i => i.Document.Id)));
        foreach (var info in infos)
        {
          if (info.NeedSign == true)
          {
            var certificate = info.RootBox.ExchangeServiceCertificates.Where(x => Equals(x.Certificate.Owner, Users.Current) && x.Certificate.Enabled == true).Select(x => x.Certificate).FirstOrDefault();
            try
            {
              if (Docflow.PublicFunctions.OfficialDocument.ApproveWithAddenda(info.Document, null, certificate, string.Empty, null, false, null))
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