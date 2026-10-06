namespace MwabuLearn.Application.Curricula;

public interface ICurriculumService
{
    Task<IReadOnlyList<CurriculumResponse>> ListAsync(CancellationToken cancellationToken);
    Task<CurriculumResponse> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<CurriculumHierarchyResponse> GetHierarchyAsync(Guid id, CancellationToken cancellationToken);
    Task<CurriculumResponse> CreateAsync(CurriculumRequest request, CancellationToken cancellationToken);
    Task<CurriculumResponse> UpdateAsync(Guid id, CurriculumRequest request, CancellationToken cancellationToken);
    Task SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken);
    Task<StructureResponse> GetCurriculumVersionAsync(Guid parentId, Guid id, CancellationToken cancellationToken);
    Task<StructureResponse> CreateCurriculumVersionAsync(Guid parentId, StructureRequest request, CancellationToken cancellationToken);
    Task<StructureResponse> UpdateCurriculumVersionAsync(Guid parentId, Guid id, StructureRequest request, CancellationToken cancellationToken);
    Task<StructureResponse> GetGradeAsync(Guid parentId, Guid id, CancellationToken cancellationToken);
    Task<StructureResponse> CreateGradeAsync(Guid parentId, StructureRequest request, CancellationToken cancellationToken);
    Task<StructureResponse> UpdateGradeAsync(Guid parentId, Guid id, StructureRequest request, CancellationToken cancellationToken);
    Task<StructureResponse> GetSubjectAsync(Guid parentId, Guid id, CancellationToken cancellationToken);
    Task<StructureResponse> CreateSubjectAsync(Guid parentId, StructureRequest request, CancellationToken cancellationToken);
    Task<StructureResponse> UpdateSubjectAsync(Guid parentId, Guid id, StructureRequest request, CancellationToken cancellationToken);
    Task<StructureResponse> GetTermAsync(Guid parentId, Guid id, CancellationToken cancellationToken);
    Task<StructureResponse> CreateTermAsync(Guid parentId, StructureRequest request, CancellationToken cancellationToken);
    Task<StructureResponse> UpdateTermAsync(Guid parentId, Guid id, StructureRequest request, CancellationToken cancellationToken);
    Task<StructureResponse> GetTopicAsync(Guid parentId, Guid id, CancellationToken cancellationToken);
    Task<StructureResponse> CreateTopicAsync(Guid parentId, StructureRequest request, CancellationToken cancellationToken);
    Task<StructureResponse> UpdateTopicAsync(Guid parentId, Guid id, StructureRequest request, CancellationToken cancellationToken);
    Task<StructureResponse> GetCompetencyAsync(Guid parentId, Guid id, CancellationToken cancellationToken);
    Task<StructureResponse> CreateCompetencyAsync(Guid parentId, StructureRequest request, CancellationToken cancellationToken);
    Task<StructureResponse> UpdateCompetencyAsync(Guid parentId, Guid id, StructureRequest request, CancellationToken cancellationToken);
    Task<StructureResponse> GetLearningOutcomeAsync(Guid parentId, Guid id, CancellationToken cancellationToken);
    Task<StructureResponse> CreateLearningOutcomeAsync(Guid parentId, StructureRequest request, CancellationToken cancellationToken);
    Task<StructureResponse> UpdateLearningOutcomeAsync(Guid parentId, Guid id, StructureRequest request, CancellationToken cancellationToken);
}