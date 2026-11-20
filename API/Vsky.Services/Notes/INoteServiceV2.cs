using Vsky.Models;

namespace Vsky.Services.Note
{
    // Read-only addition for the chat-style notes thread (v2). Saving and deleting notes stay on INoteService.
    public interface INoteServiceV2
    {
        NoteThreadV2 GetNoteThread(string siteId, string subModuleId, string type, string noteTypeId, string beforeId, int take);
    }
}
