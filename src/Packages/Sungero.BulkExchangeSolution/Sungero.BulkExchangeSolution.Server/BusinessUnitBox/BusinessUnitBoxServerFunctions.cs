using System;
using System.Collections.Generic;
using System.Linq;
using Sungero.Core;
using Sungero.CoreEntities;
using Sungero.BulkExchangeSolution.BusinessUnitBox;

namespace Sungero.BulkExchangeSolution.Server
{
  partial class BusinessUnitBoxFunctions
  {
    public override Sungero.Company.IEmployee GetExchangeDocumentResponsible(Sungero.Parties.ICounterparty counterparty, List<Sungero.Exchange.IExchangeDocumentInfo> infos)
    {
      return GetResponsibleForAutoRouting(_obj, counterparty, infos);
    }
    
    public static Sungero.Company.IEmployee GetResponsibleForAutoRouting(ExchangeCore.IBoxBase box, Sungero.Parties.ICounterparty counterparty, List<Sungero.Exchange.IExchangeDocumentInfo> infos)
    {
      if (infos != null)
      {
        var documentSets = Functions.ExchangeDocumentInfo.GetDocumentSets(infos.OfType<IExchangeDocumentInfo>().ToList());
        foreach (var documentSet in documentSets.Where(s => s.IsFullSet))
        {
          if (documentSet.Type == Constants.Exchange.ExchangeDocumentInfo.DocumentSetType.ContractStatement)
          {
            // Раз комплект корректный - значит номер один и на него существует один договор.
            var contractNumber = documentSet.ExchangeDocumentInfos
              .Select(i => i.ContractNumber)
              .FirstOrDefault();
            var contract = Sungero.Contracts.ContractBases
              .GetAll(c => Equals(counterparty, c.Counterparty) && c.RegistrationNumber == contractNumber)
              .FirstOrDefault();
            if (contract != null && contract.ResponsibleEmployee != null)
              return contract.ResponsibleEmployee;
          }
        }
      }
      
      var company = Parties.CompanyBases.As(counterparty);
      if (company != null && company.Responsible != null)
        return company.Responsible;
      else
        throw AppliedCodeException.Create(string.Format("#1: Не указан ответственный за контрагента {0}", counterparty.Name));
    }
  }
}