using System;
using System.Collections.Generic;
using System.Linq;
using Sungero.Core;
using Sungero.CoreEntities;

namespace Sungero.BulkExchangeSolution.Module.Exchange.Shared
{
  partial class ModuleFunctions
  {
    [Public]
    public string GetContractNumberFromDocumentName(string documentName)
    {
      if (documentName.ToLowerInvariant().Contains("акт"))
      {
        var pattern = @"(^|[^А-Яа-я])акт([^А-Яа-я]|$)";
        if (System.Text.RegularExpressions.Regex.IsMatch(documentName.ToLower(), pattern))
        {
          var noteArray  = documentName.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
          var index = Array.IndexOf(noteArray, Constants.Module.ContractNumber);
          if (index != -1)
          {
            var contractNumber = noteArray.ElementAtOrDefault(index + 1);
            if (contractNumber != null)
              return contractNumber;
          }
        }
      }
      
      return string.Empty;
    }
  }
}
