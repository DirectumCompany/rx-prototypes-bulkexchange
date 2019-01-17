using System;
using Sungero.Core;

namespace Sungero.BulkExchangeSolution.Constants.Exchange
{
  public static class ExchangeDocumentInfo
  {
    public static class DocumentSetType
    {
      // Товарный комплект.
      public const string Waybill = "Waybill";
      
      // Нетоварный комплект.
      public const string ContractStatement = "ContractStatement";
    }
  }
}