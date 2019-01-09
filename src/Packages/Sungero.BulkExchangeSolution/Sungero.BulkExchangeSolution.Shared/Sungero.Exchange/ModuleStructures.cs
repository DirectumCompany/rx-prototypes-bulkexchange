using System;
using System.Collections.Generic;
using System.Linq;
using Sungero.Core;
using Sungero.CoreEntities;

namespace Sungero.BulkExchangeSolution.Module.Exchange.Structures.Module
{
  partial class DocumentSet
  {
    public bool FullSet { get; set; }
    
    public List<Sungero.BulkExchangeSolution.IExchangeDocumentInfo> ExchangeDocumentInfos { get; set; }
  }
}