using System;
using System.Collections.Generic;
using System.Linq;
using Sungero.Core;
using Sungero.CoreEntities;
using Sungero.BulkExchangeSolution.BusinessUnitBox;

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