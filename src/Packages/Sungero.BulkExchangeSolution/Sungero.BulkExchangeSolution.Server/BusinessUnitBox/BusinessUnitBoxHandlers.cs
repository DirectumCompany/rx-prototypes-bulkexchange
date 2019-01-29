using System;
using System.Collections.Generic;
using System.Linq;
using Sungero.BulkExchangeSolution.BusinessUnitBox;
using Sungero.Core;
using Sungero.CoreEntities;

namespace Sungero.BulkExchangeSolution
{
  partial class BusinessUnitBoxServerHandlers
  {

    public override void Created(Sungero.Domain.CreatedEventArgs e)
    {
      base.Created(e);
      _obj.Routing = Routing.Auto;
    }
  }

}