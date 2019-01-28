using System;
using System.Collections.Generic;
using System.Linq;
using Sungero.Core;
using Sungero.CoreEntities;
using Sungero.Domain.Initialization;

namespace Sungero.BulkExchangeSolution.Module.FinancialArchiveUI.Server
{
  public partial class ModuleInitializer
  {

    public override void Initializing(Sungero.Domain.ModuleInitializingEventArgs e)
    {
      base.Initializing(e);
      GrantRightOnFolders(Roles.AllUsers);
    }
    
    /// <summary>
    /// Выдать права на спец.папки.
    /// </summary>
    /// <param name="role">Роль.</param>
    public static void GrantRightOnFolders(IRole role)
    {
      var hasLicense = Docflow.PublicFunctions.Module.Remote.IsModuleAvailableByLicense(Guid.Parse("e99ae7e2-edb7-4904-a19a-4577f07609a4"));
      Dictionary<int, byte[]> licenses = null;
      
      try
      {
        if (!hasLicense)
        {
          licenses = Docflow.PublicFunctions.Module.ReadLicense();
          Docflow.PublicFunctions.Module.DeleteLicense();
        }
        
        // Права на папку "На подписание".
        FinancialArchiveUI.SpecialFolders.ForSignature.AccessRights.Grant(role, DefaultAccessRightsTypes.Read);
        FinancialArchiveUI.SpecialFolders.ForSignature.AccessRights.Save();
      }
      finally
      {
        Docflow.PublicFunctions.Module.RestoreLicense(licenses);
      }
    }
  }
}
