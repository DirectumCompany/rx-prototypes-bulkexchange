using System;
using System.Collections.Generic;
using System.Linq;
using Sungero.Core;
using Sungero.CoreEntities;
using Sungero.BulkExchangeSolution.Waybill;

namespace Sungero.BulkExchangeSolution.Client
{
  partial class WaybillActions
  {
    public virtual void Reject(Sungero.Domain.Client.ExecuteActionArgs e)
    {
      Functions.Module.RejectDocument(_obj);
    }

    public virtual bool CanReject(Sungero.Domain.Client.CanExecuteActionArgs e)
    {
      return true;
    }

  }

}