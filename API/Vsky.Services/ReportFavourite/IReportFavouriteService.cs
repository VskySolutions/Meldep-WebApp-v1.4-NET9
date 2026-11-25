using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vsky.Models;

namespace Vsky.Services.ReportFavourite
{
    public interface IReportFavouriteService
    {
        #region GetReportFavouriteByUser
        Task<Models.ReportFavourite> GetReportFavouriteByUser(string reportSettingsDetailId, string LoggedUserId);
        #endregion

        #region InsertReportFavourite
        void InsertReportFavourite(Models.ReportFavourite entity);
        #endregion
    }
}
