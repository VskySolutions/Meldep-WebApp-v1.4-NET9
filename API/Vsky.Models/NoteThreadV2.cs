using System.Collections.Generic;

namespace Vsky.Models
{
    // Read-side shape of a record's notes read like a chat: one page of the newest notes (or of the notes
    // older than a cursor), with the per-type counts the filter chips show. Not mapped to a table.
    public class NoteThreadV2
    {
        /// <summary>The page, newest first.</summary>
        public List<Notes> Items { get; set; } = new List<Notes>();

        /// <summary>Whether older notes exist beyond this page (within the same type filter).</summary>
        public bool HasMore { get; set; }

        /// <summary>How many notes match the type filter in all.</summary>
        public int Total { get; set; }

        /// <summary>How many notes of each type the record has, regardless of the filter.</summary>
        public List<NoteTypeCountV2> CountsByType { get; set; } = new List<NoteTypeCountV2>();
    }

    public class NoteTypeCountV2
    {
        public string NoteTypeId { get; set; }
        public int Count { get; set; }
    }
}
