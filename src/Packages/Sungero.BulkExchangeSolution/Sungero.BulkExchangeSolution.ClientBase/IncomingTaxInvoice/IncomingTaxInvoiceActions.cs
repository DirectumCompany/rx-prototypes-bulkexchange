using System;
using System.Collections.Generic;
using System.Linq;
using Sungero.Core;
using Sungero.CoreEntities;
using Sungero.BulkExchangeSolution.IncomingTaxInvoice;

namespace Sungero.BulkExchangeSolution.Client
{
  partial class IncomingTaxInvoiceActions
  {
    public virtual void Reject(Sungero.Domain.Client.ExecuteActionArgs e)
    {
      
    }

    public virtual bool CanReject(Sungero.Domain.Client.CanExecuteActionArgs e)
    {
      return true;
    }

  }

}