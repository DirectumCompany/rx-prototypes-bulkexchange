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
  }
}