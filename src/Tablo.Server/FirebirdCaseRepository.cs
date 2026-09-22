using FirebirdSql.Data.FirebirdClient;
using Tablo.Shared;

namespace Tablo.Server;

/// <summary>
/// Доступ к данным Firebird. Собирает сегодняшние заседания из веток
/// G33_* (гражданские + административные, различаются по CASE_TYPE_ID) и
/// U33_* (уголовные, отдельная ветка таблиц целиком).
/// </summary>
public sealed class FirebirdCaseRepository
{
    // CASE_TYPE_ID в G33_PROCEEDING.
    private const int CivilCaseTypeId = 910760002;
    private const int AdministrativeCaseTypeId = 910760004;

    // PARTS_TYPE_ID в G33_PARTS, которые нужно выводить на табло.
    private const int PlaintiffTypeId = 50400000;
    private const int DefendantTypeId = 50400001;
    private const int AdminPlaintiffTypeId = 50400100;
    private const int AdminDefendantTypeId = 50400101;

    private readonly string _connectionString;

    public FirebirdCaseRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<(IReadOnlyList<CaseDisplayItem> Cases, IReadOnlyList<HallInfo> Halls)> GetTodayCasesAsync(
        CancellationToken cancellationToken)
    {
        var today = DateTime.Today;
        var tomorrow = today.AddDays(1);

        await using var connection = new FbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var civilAndAdmin = await LoadCivilAndAdministrativeAsync(connection, today, tomorrow, cancellationToken);
        var criminal = await LoadCriminalAsync(connection, today, tomorrow, cancellationToken);

        var all = new List<CaseDisplayItem>(civilAndAdmin.Count + criminal.Count);
        all.AddRange(civilAndAdmin);
        all.AddRange(criminal);

        // Список залов берём из уже полученных сегодняшних данных, а не
        // отдельным запросом: ROOM_NUMBER_ID в *_EVENT не проиндексирован,
        // и запрос "все залы, когда-либо встречавшиеся в событиях" без
        // фильтра по дате оказался полным сканом многолетней истории
        // заседаний — на реальной БД это занимало минуты и стабильно
        // попадало под плановый перезапуск Firebird (падало с обрывом
        // соединения). Дат из сегодняшних событий более чем достаточно —
        // это ровно те залы, что и так показываются на табло.
        var halls = all
            .Where(c => c.HallId is not null && !string.IsNullOrWhiteSpace(c.HallName))
            .GroupBy(c => c.HallId!.Value)
            .Select(g => new HallInfo(g.Key, g.First().HallName!))
            .OrderBy(h => h.Name)
            .ToList();

        return (all, halls);
    }

    private static async Task<List<CaseDisplayItem>> LoadCivilAndAdministrativeAsync(
        FbConnection connection, DateTime dayStart, DateTime dayEnd, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                e.ID                    AS EVENT_ID,
                e.ROOM_NUMBER_ID        AS HALL_ID,
                hall.NAME               AS HALL_NAME,
                p.FULL_NUMBER           AS CASE_NUMBER,
                judge.USERNAME          AS JUDGE_NAME,
                p.CASE_TYPE_ID          AS CASE_TYPE_ID,
                e.EVENT_TIME            AS EVENT_TIME,
                parts.NAME              AS PART_NAME,
                partType.NAME           AS PART_ROLE,
                parts.PARTS_TYPE_ID     AS PART_TYPE_ID,
                verdict.NAME            AS RESULT_NAME
            FROM G33_EVENT e
            JOIN G33_PROCEEDING p ON p.ID = e.PROC_ID
            LEFT JOIN CATALOGCONTENT hall ON hall.CONTENTID = e.ROOM_NUMBER_ID
            LEFT JOIN GROUPCONTENT judge ON judge.GROUPCONTENTID = p.JUDGE_SPEAKER_ID
            LEFT JOIN CATALOGCONTENT verdict ON verdict.CONTENTID = p.SATISFIED_VERDICT_I_ID
            LEFT JOIN G33_PARTS parts ON parts.PROC_ID = p.ID
                AND parts.PARTS_TYPE_ID IN (@plaintiff, @defendant, @adminPlaintiff, @adminDefendant)
            LEFT JOIN CATALOGCONTENT partType ON partType.CONTENTID = parts.PARTS_TYPE_ID
            WHERE e.EVENT_DATE >= @dayStart AND e.EVENT_DATE < @dayEnd
                AND p.CASE_TYPE_ID IN (@civil, @administrative)
            ORDER BY e.ID
            """;

        await using var command = new FbCommand(sql, connection);
        command.Parameters.Add("@plaintiff", FbDbType.Integer).Value = PlaintiffTypeId;
        command.Parameters.Add("@defendant", FbDbType.Integer).Value = DefendantTypeId;
        command.Parameters.Add("@adminPlaintiff", FbDbType.Integer).Value = AdminPlaintiffTypeId;
        command.Parameters.Add("@adminDefendant", FbDbType.Integer).Value = AdminDefendantTypeId;
        command.Parameters.Add("@dayStart", FbDbType.TimeStamp).Value = dayStart;
        command.Parameters.Add("@dayEnd", FbDbType.TimeStamp).Value = dayEnd;
        command.Parameters.Add("@civil", FbDbType.Integer).Value = CivilCaseTypeId;
        command.Parameters.Add("@administrative", FbDbType.Integer).Value = AdministrativeCaseTypeId;

        var result = new List<CaseDisplayItem>();
        long? currentEventId = null;
        int? hallId = null;
        string? hallName = null;
        string caseNumber = "";
        string? judgeName = null;
        int? caseTypeId = null;
        TimeSpan? eventTime = null;
        string? resultName = null;
        var parties = new List<PartyLine>();

        void Flush()
        {
            if (currentEventId is null) return;
            var category = caseTypeId == AdministrativeCaseTypeId
                ? CaseCategory.Administrative
                : CaseCategory.Civil;
            result.Add(new CaseDisplayItem(hallId, hallName, caseNumber, judgeName, category, eventTime, parties, resultName));
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var eventId = reader.GetInt64(reader.GetOrdinal("EVENT_ID"));
            if (currentEventId != eventId)
            {
                Flush();
                currentEventId = eventId;
                hallId = reader.IsDBNull(reader.GetOrdinal("HALL_ID")) ? null : reader.GetInt32(reader.GetOrdinal("HALL_ID"));
                hallName = reader.IsDBNull(reader.GetOrdinal("HALL_NAME")) ? null : reader.GetString(reader.GetOrdinal("HALL_NAME"));
                caseNumber = reader.IsDBNull(reader.GetOrdinal("CASE_NUMBER")) ? "" : reader.GetString(reader.GetOrdinal("CASE_NUMBER"));
                judgeName = reader.IsDBNull(reader.GetOrdinal("JUDGE_NAME")) ? null : reader.GetString(reader.GetOrdinal("JUDGE_NAME"));
                caseTypeId = reader.IsDBNull(reader.GetOrdinal("CASE_TYPE_ID")) ? null : reader.GetInt32(reader.GetOrdinal("CASE_TYPE_ID"));
                eventTime = reader.IsDBNull(reader.GetOrdinal("EVENT_TIME")) ? null : reader.GetDateTime(reader.GetOrdinal("EVENT_TIME")).TimeOfDay;
                resultName = reader.IsDBNull(reader.GetOrdinal("RESULT_NAME")) ? null : reader.GetString(reader.GetOrdinal("RESULT_NAME"));
                parties = new List<PartyLine>();
            }

            if (!reader.IsDBNull(reader.GetOrdinal("PART_NAME")))
            {
                var partName = reader.GetString(reader.GetOrdinal("PART_NAME"));
                var partRole = reader.IsDBNull(reader.GetOrdinal("PART_ROLE")) ? null : reader.GetString(reader.GetOrdinal("PART_ROLE"));
                parties.Add(new PartyLine(partRole, partName, null));
            }
        }
        Flush();

        return result;
    }

    private static async Task<List<CaseDisplayItem>> LoadCriminalAsync(
        FbConnection connection, DateTime dayStart, DateTime dayEnd, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                e.ID                    AS EVENT_ID,
                e.ROOM_NUMBER_ID        AS HALL_ID,
                hall.NAME               AS HALL_NAME,
                p.FULL_NUMBER           AS CASE_NUMBER,
                judge.USERNAME          AS JUDGE_NAME,
                e.EVENT_TIME            AS EVENT_TIME,
                parts.NAME              AS PART_NAME,
                parts.MAIN_LAW_ARTICLE  AS PART_ARTICLE,
                verdict.NAME            AS RESULT_NAME
            FROM U33_EVENT e
            JOIN U33_PROCEEDING p ON p.ID = e.PROC_ID
            LEFT JOIN CATALOGCONTENT hall ON hall.CONTENTID = e.ROOM_NUMBER_ID
            LEFT JOIN GROUPCONTENT judge ON judge.GROUPCONTENTID = p.JUDGE_SPEAKER_ID
            LEFT JOIN CATALOGCONTENT verdict ON verdict.CONTENTID = p.VERDICT_BY_PROTEST
            LEFT JOIN U33_PARTS parts ON parts.PROC_ID = p.ID
            WHERE e.EVENT_DATE >= @dayStart AND e.EVENT_DATE < @dayEnd
            ORDER BY e.ID
            """;

        await using var command = new FbCommand(sql, connection);
        command.Parameters.Add("@dayStart", FbDbType.TimeStamp).Value = dayStart;
        command.Parameters.Add("@dayEnd", FbDbType.TimeStamp).Value = dayEnd;

        var result = new List<CaseDisplayItem>();
        long? currentEventId = null;
        int? hallId = null;
        string? hallName = null;
        string caseNumber = "";
        string? judgeName = null;
        TimeSpan? eventTime = null;
        string? resultName = null;
        var parties = new List<PartyLine>();

        void Flush()
        {
            if (currentEventId is null) return;
            result.Add(new CaseDisplayItem(hallId, hallName, caseNumber, judgeName, CaseCategory.Criminal, eventTime, parties, resultName));
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var eventId = reader.GetInt64(reader.GetOrdinal("EVENT_ID"));
            if (currentEventId != eventId)
            {
                Flush();
                currentEventId = eventId;
                hallId = reader.IsDBNull(reader.GetOrdinal("HALL_ID")) ? null : reader.GetInt32(reader.GetOrdinal("HALL_ID"));
                hallName = reader.IsDBNull(reader.GetOrdinal("HALL_NAME")) ? null : reader.GetString(reader.GetOrdinal("HALL_NAME"));
                caseNumber = reader.IsDBNull(reader.GetOrdinal("CASE_NUMBER")) ? "" : reader.GetString(reader.GetOrdinal("CASE_NUMBER"));
                judgeName = reader.IsDBNull(reader.GetOrdinal("JUDGE_NAME")) ? null : reader.GetString(reader.GetOrdinal("JUDGE_NAME"));
                eventTime = reader.IsDBNull(reader.GetOrdinal("EVENT_TIME")) ? null : reader.GetDateTime(reader.GetOrdinal("EVENT_TIME")).TimeOfDay;
                resultName = reader.IsDBNull(reader.GetOrdinal("RESULT_NAME")) ? null : reader.GetString(reader.GetOrdinal("RESULT_NAME"));
                parties = new List<PartyLine>();
            }

            if (!reader.IsDBNull(reader.GetOrdinal("PART_NAME")))
            {
                var partName = reader.GetString(reader.GetOrdinal("PART_NAME"));
                var article = reader.IsDBNull(reader.GetOrdinal("PART_ARTICLE")) ? null : reader.GetString(reader.GetOrdinal("PART_ARTICLE"));
                parties.Add(new PartyLine(null, partName, article));
            }
        }
        Flush();

        return result;
    }
}
