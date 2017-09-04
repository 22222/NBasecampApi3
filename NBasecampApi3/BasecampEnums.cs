using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections;
using System.ComponentModel;
using System.Threading;

namespace NBasecampApi3
{
    public enum ProjectStatusEnum
    {
        [Description("")] Unknown,
        [Description("archived")] Archived,
        [Description("trashed")] Trashed
    }

    public enum RecordingTypeEnum
    {
        [Description("")] Unknown,
        Comment,
        Document,
        Message,
        [Description("Question::Answer")] QuestionAnswer,
        [Description("Schedule::Entry")] ScheduleEntry,
        Todo,
        Todolist,
        Upload
    }

    public enum RecordingStatusEnum
    {
        [Description("")] Unknown,
        [Description("active")] Active,
        [Description("archived")] Archived,
        [Description("trashed")] Trashed
    }

    public enum RecordingSortEnum
    {
        [Description("")] Unknown,
        [Description("created_at")] CreatedAt,
        [Description("updated_at")] UpdatedAt
    }

    public enum SortDirection
    {
        [Description("")] Unknown,
        [Description("desc")] Desc,
        [Description("asc")] Asc
    }
}
