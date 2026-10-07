using Microsoft.EntityFrameworkCore;
using MwabuLearn.Application.Curricula;
using MwabuLearn.Domain.Entities;
using MwabuLearn.Infrastructure.Persistence;
using Npgsql;

namespace MwabuLearn.Infrastructure.Curricula;

public sealed partial class CurriculumService(MwabuDbContext db) : ICurriculumService
{
    // ISO 3166-1 alpha-2 assignment list, independent of installed OS cultures.
    private static readonly HashSet<string> CountryCodes = ("AD AE AF AG AI AL AM AO AQ AR AS AT AU AW AX AZ " +
        "BA BB BD BE BF BG BH BI BJ BL BM BN BO BQ BR BS BT BV BW BY BZ CA CC CD CF CG CH CI CK CL CM CN CO CR CU CV CW CX CY CZ " +
        "DE DJ DK DM DO DZ EC EE EG EH ER ES ET FI FJ FK FM FO FR GA GB GD GE GF GG GH GI GL GM GN GP GQ GR GS GT GU GW GY " +
        "HK HM HN HR HT HU ID IE IL IM IN IO IQ IR IS IT JE JM JO JP KE KG KH KI KM KN KP KR KW KY KZ " +
        "LA LB LC LI LK LR LS LT LU LV LY MA MC MD ME MF MG MH MK ML MM MN MO MP MQ MR MS MT MU MV MW MX MY MZ " +
        "NA NC NE NF NG NI NL NO NP NR NU NZ OM PA PE PF PG PH PK PL PM PN PR PS PT PW PY QA RE RO RS RU RW " +
        "SA SB SC SD SE SG SH SI SJ SK SL SM SN SO SR SS ST SV SX SY SZ TC TD TF TG TH TJ TK TL TM TN TO TR TT TV TW TZ " +
        "UA UG UM US UY UZ VA VC VE VG VI VN VU WF WS YE YT ZA ZM ZW").Split(' ').ToHashSet(StringComparer.Ordinal);

    public async Task<IReadOnlyList<CurriculumResponse>> ListAsync(CancellationToken cancellationToken)
    {
        await using var snapshot = await ReadSnapshot(cancellationToken);
        if (await db.Curricula.Select(x => x.Id).Take(1001).CountAsync(cancellationToken) > 1000 ||
            await db.CurriculumVersions.Select(x => x.Id).Take(5001).CountAsync(cancellationToken) > 5000)
            throw new MwabuLearn.Application.Directories.LegacyResultLimitException();
        var curricula = await db.Curricula.AsNoTracking().AsSplitQuery().Include(x => x.CurriculumVersions)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Name).ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);
        return curricula.Select(MapCurriculum).ToList();
    }

    public async Task<CurriculumResponse> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var snapshot = await ReadSnapshot(cancellationToken);
        if (await db.CurriculumVersions.Where(x => x.CurriculumId == id).Select(x => x.Id).Take(1001).CountAsync(cancellationToken) > 1000)
            throw new MwabuLearn.Application.Directories.LegacyResultLimitException();
        var curriculum = await db.Curricula.AsNoTracking().Include(x => x.CurriculumVersions)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken) ?? throw Missing("Curriculum");
        return MapCurriculum(curriculum);
    }

    public async Task<CurriculumHierarchyResponse> GetHierarchyAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var snapshot = await ReadSnapshot(cancellationToken);
        await EnsureBoundedHierarchy(id, cancellationToken);
        var curriculum = await db.Curricula.AsNoTracking().AsSplitQuery()
            .Include(x => x.CurriculumVersions).ThenInclude(x => x.Grades).ThenInclude(x => x.Subjects)
            .ThenInclude(x => x.Terms).ThenInclude(x => x.Topics).ThenInclude(x => x.Competencies)
            .ThenInclude(x => x.LearningOutcomes)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken) ?? throw Missing("Curriculum");
        return MapHierarchy(curriculum);
    }

    public async Task<CurriculumResponse> CreateAsync(CurriculumRequest request, CancellationToken cancellationToken)
    {
        var values = Validate(request);
        var country = ValidateCountry(request.CountryCode);
        await CheckCurriculumDuplicate(country, values.Name, values.Code, null, cancellationToken);
        var entity = new Curriculum();
        Apply(entity, values, request);
        entity.CountryCode = country;
        db.Curricula.Add(entity);
        await Save(cancellationToken);
        return MapCurriculum(entity);
    }

    public async Task<CurriculumResponse> UpdateAsync(Guid id, CurriculumRequest request, CancellationToken cancellationToken)
    {
        var values = Validate(request);
        var country = ValidateCountry(request.CountryCode);
        var entity = await db.Curricula.Include(x => x.CurriculumVersions)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken) ?? throw Missing("Curriculum");
        await CheckCurriculumDuplicate(country, values.Name, values.Code, id, cancellationToken);
        Apply(entity, values, request);
        entity.CountryCode = country;
        entity.UpdatedAt = DateTime.UtcNow;
        await Save(cancellationToken);
        return MapCurriculum(entity);
    }

    public async Task SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken)
    {
        var entity = await db.Curricula.SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw Missing("Curriculum");
        entity.IsActive = isActive;
        entity.UpdatedAt = DateTime.UtcNow;
        await Save(cancellationToken);
    }

    private async Task CheckCurriculumDuplicate(string country, string name, string? code, Guid? id, CancellationToken ct)
    {
        var normalizedName = name.ToUpperInvariant();
        if (await db.Curricula.AnyAsync(x => x.Id != id && x.CountryCode == country &&
            (x.NormalizedName == normalizedName || (code != null && x.Code == code)), ct))
            throw Duplicate();
    }

    private async Task Save(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw Duplicate();
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation })
        {
            throw new CurriculumException(CurriculumError.Conflict, "The parent structure changed. Reload and retry.");
        }
    }

    private static (string Name, string? Code, string? Description) Validate(StructureRequest request)
    {
        var name = request.Name?.Trim() ?? "";
        var code = NullIfEmpty(request.Code)?.ToUpperInvariant();
        var description = NullIfEmpty(request.Description);
        if (name.Length is 0 or > 200 || code?.Length > 50 || description?.Length > 4000 || request.SortOrder < 0)
            throw new CurriculumException(CurriculumError.Validation,
                "Name is required (maximum 200 characters); code allows 50, description 4000, and sort order must be nonnegative.");
        return (name, code, description);
    }

    private static string ValidateCountry(string countryCode)
    {
        var country = countryCode?.Trim().ToUpperInvariant() ?? "";
        if (!CountryCodes.Contains(country))
            throw new CurriculumException(CurriculumError.Validation, "CountryCode must be an ISO alpha-2 country code.");
        return country;
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static CurriculumException Missing(string type) => new(CurriculumError.NotFound, $"{type} was not found under the specified parent.");
    private static CurriculumException Duplicate() => new(CurriculumError.Conflict, "A structure with that name or code already exists under this parent.");
}
