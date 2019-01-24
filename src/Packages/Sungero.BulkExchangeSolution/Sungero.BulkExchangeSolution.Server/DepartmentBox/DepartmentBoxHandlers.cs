using System;
using System.Collections.Generic;
using System.Linq;
using Sungero.Core;
using Sungero.CoreEntities;
using Sungero.BulkExchangeSolution.DepartmentBox;

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