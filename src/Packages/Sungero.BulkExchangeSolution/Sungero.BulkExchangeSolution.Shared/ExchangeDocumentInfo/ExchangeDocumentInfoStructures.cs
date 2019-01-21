using System;
using System.Collections.Generic;
using System.Linq;
using Sungero.Core;
using Sungero.CoreEntities;

namespace Sungero.BulkExchangeSolution.Structures.Exchange.ExchangeDocumentInfo
{
  partial class DocumentSet
  {
    public bool IsFullSet { get; set; }
    
    public List<Sungero.BulkExchangeSolution.IExchangeDocumentInfo> ExchangeDocumentInfos { get; set; }
    
    public string Type { get; set; }
  }
}