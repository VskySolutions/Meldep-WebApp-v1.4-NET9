using System;
using System.Linq;
using Vsky.Data;
using Vsky.Models;

namespace Vsky.Services.Note
{
    public class NoteServiceV2 : INoteServiceV2
    {
        private readonly IRepository<Notes> _noteRepository;

        public NoteServiceV2(IRepository<Notes> noteRepository)
        {
            _noteRepository = noteRepository;
        }

        #region GetNoteThread
        // The thread is read like a chat: the newest page first, older pages as the reader scrolls up. The
        // cursor is the id of the oldest note already shown, resolved here to its (CreatedOnUtc, Id) so notes
        // written in the meantime cannot shift the pages.
        public NoteThreadV2 GetNoteThread(string siteId, string subModuleId, string type, string noteTypeId, string beforeId, int take)
        {
            take = Math.Clamp(take, 1, 100);

            var all = _noteRepository.TableNoTracking
                .Where(x => x.SiteId == siteId && x.SubModuleId == subModuleId && x.Type == type && !x.Deleted);

            var countsByType = all
                .GroupBy(x => x.NoteTypeId)
                .Select(g => new NoteTypeCountV2 { NoteTypeId = g.Key, Count = g.Count() })
                .ToList();

            var query = string.IsNullOrEmpty(noteTypeId) ? all : all.Where(x => x.NoteTypeId == noteTypeId);
            var total = query.Count();

            if (!string.IsNullOrEmpty(beforeId))
            {
                var cursor = all.Where(x => x.Id == beforeId).Select(x => new { x.CreatedOnUtc, x.Id }).FirstOrDefault();
                if (cursor != null)
                {
                    query = query.Where(x =>
                        x.CreatedOnUtc < cursor.CreatedOnUtc ||
                        (x.CreatedOnUtc == cursor.CreatedOnUtc && string.Compare(x.Id, cursor.Id) < 0));
                }
            }

            // One more than asked tells whether another page exists without a second count.
            var items = query
                .OrderByDescending(x => x.CreatedOnUtc)
                .ThenByDescending(x => x.Id)
                .Take(take + 1)
                .Select(x => new Notes
                {
                    Id = x.Id,
                    ModuleId = x.ModuleId,
                    Module = x.Module,
                    SubModuleId = x.SubModuleId,
                    Sub_Module = x.Sub_Module,
                    Type = x.Type,
                    Note = x.Note,
                    NoteTypeId = x.NoteTypeId,
                    CreatedById = x.CreatedById,
                    CreatedOnUtc = x.CreatedOnUtc,
                    UpdatedById = x.UpdatedById,
                    UpdatedOnUtc = x.UpdatedOnUtc,
                    NoteType = new DropDown
                    {
                        Id = x.NoteType.Id,
                        DropDownValue = x.NoteType.DropDownValue
                    },
                    User = new ApplicationUser
                    {
                        Id = x.User.Id,
                        UserName = x.User.UserName,
                        Person = new Person
                        {
                            Id = x.User.PersonId,
                            FirstName = x.User.Person.FirstName,
                            LastName = x.User.Person.LastName,
                            FullName = x.User.Person.FirstName + " " + x.User.Person.LastName
                        }
                    }
                })
                .ToList();

            var hasMore = items.Count > take;
            if (hasMore) items.RemoveAt(items.Count - 1);

            return new NoteThreadV2 { Items = items, HasMore = hasMore, Total = total, CountsByType = countsByType };
        }
        #endregion
    }
}
