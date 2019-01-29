using System;
using System.Collections.Generic;
using System.Linq;
using Sungero.BulkExchangeSolution.DepartmentBox;
using Sungero.Core;
using Sungero.CoreEntities;

namespace Sungero.BulkExchangeSolution.Server
{
  partial class DepartmentBoxFunctions
  {
    public override Sungero.Company.IEmployee GetExchangeDocumentResponsible(Sungero.Parties.ICounterparty counterparty, List<Sungero.Exchange.IExchangeDocumentInfo> infos)
    {
      return Functions.BusinessUnitBox.GetResponsibleForAutoRouting(_obj, counterparty, infos);
    }
  }
}