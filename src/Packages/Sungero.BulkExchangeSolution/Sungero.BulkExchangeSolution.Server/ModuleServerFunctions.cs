using System;
using System.Collections.Generic;
using System.Linq;
using Sungero.BulkExchangeSolution.ExchangeDocumentInfo;
using Sungero.Core;
using Sungero.CoreEntities;

namespace Sungero.BulkExchangeSolution.Server
{
  public class ModuleFunctions
  {
    [Remote(IsPure = true)]
    public IQueryable<IExchangeDocumentInfo> GetRejectedDocumentInfos()
    {
      return ExchangeDocumentInfos.GetAll(x => x.RejectionStatus == RejectionStatus.Required);
    }
    
    /// <summary>
    /// Получить комплекты документов требующие подписания и прошедщие сверку.
    /// </summary>
    /// <returns>Список комплектов документов.</returns>
    [Remote]
    public virtual List<Sungero.BulkExchangeSolution.Structures.Exchange.ExchangeDocumentInfo.DocumentSet> GetVerifiedSets()
    {
      var boxes = Sungero.ExchangeCore.PublicFunctions.BusinessUnitBox.Remote.GetConnectedBoxes().Where(c => c.HasExchangeServiceCertificates == true &&
                                                                                                        c.ExchangeServiceCertificates.Any(x => Equals(x.Certificate.Owner, Users.Current) &&
                                                                                                                                          x.Certificate.Enabled == true)).ToList();
      var infos = ExchangeDocumentInfos.GetAll()
        .Where(x => boxes.Contains(x.RootBox) && x.ExchangeState == Sungero.BulkExchangeSolution.ExchangeDocumentInfo.ExchangeState.SignRequired &&
               (x.SignStatus == null || x.SignStatus != SignStatus.Signed) && x.VerificationStatus == Sungero.BulkExchangeSolution.ExchangeDocumentInfo.VerificationStatus.Completed);
      
      return Sungero.BulkExchangeSolution.Functions.ExchangeDocumentInfo.GetDocumentSets(infos.ToList()).Where(x => x.IsFullSet).ToList();
    }
  }
}