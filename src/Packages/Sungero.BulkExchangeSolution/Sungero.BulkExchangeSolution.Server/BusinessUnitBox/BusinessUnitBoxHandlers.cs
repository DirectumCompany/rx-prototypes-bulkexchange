﻿using System;
using System.Collections.Generic;
using System.Linq;
using Sungero.BulkExchangeSolution.BusinessUnitBox;
using Sungero.Core;
using Sungero.CoreEntities;

namespace Sungero.BulkExchangeSolution
{
  partial class BusinessUnitBoxSignDocumentCertificatePropertyFilteringServerHandler<T>
  {

    public virtual IQueryable<T> SignDocumentCertificateFiltering(IQueryable<T> query, Sungero.Domain.PropertyFilteringEventArgs e)
    {
      var exchangeBoxCertificates = _obj.ExchangeServiceCertificates.Select(c => c.Certificate).ToList();
      return query.Where(x => exchangeBoxCertificates.Contains(x));
    }
  }

}