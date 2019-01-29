using System;
using System.Collections.Generic;
using System.Linq;
using Sungero.BulkExchangeSolution.DepartmentBox;
using Sungero.Core;
using Sungero.CoreEntities;

namespace Sungero.BulkExchangeSolution
{
  partial class DepartmentBoxServerHandlers
  {

    public override void Created(Sungero.Domain.CreatedEventArgs e)
    {
      base.Created(e);
      _obj.Routing = Routing.Auto;
    }
  }

}