using Climate.Audit.Application.Abstractions;
using Climate.Audit.Application.Auditing;
using Climate.Audit.Domain.Auditing;
using Climate.Contracts.Audit;

namespace Climate.Audit.Tests.Auditing;

public sealed class AuditServiceTests
{
    [Fact]
    public async Task RecordPersistsAdministrativeAction()
    {
        var repository=new FakeRepository(); RecordAuditRequest request=CreateRequest();
        var result=await new AuditService(repository).RecordAsync(request,CancellationToken.None);
        Assert.True(result.IsSuccess); Assert.Equal(AuditActions.CreateSensor,result.Value.Action); Assert.Single(repository.Items);
    }
    [Fact]
    public async Task DuplicateEventIsIdempotent()
    {
        var repository=new FakeRepository(); RecordAuditRequest request=CreateRequest(); var service=new AuditService(repository);
        await service.RecordAsync(request,CancellationToken.None); await service.RecordAsync(request,CancellationToken.None);
        Assert.Single(repository.Items);
    }
    [Fact]
    public async Task InvalidAuditIsRejected()
    {
        var result=await new AuditService(new FakeRepository()).RecordAsync(CreateRequest() with{UserId=Guid.Empty},CancellationToken.None);
        Assert.Equal(AuditErrors.Invalid,result.Error);
    }
    [Fact]
    public async Task MissingAuditReturnsNotFound()
    {
        var result=await new AuditService(new FakeRepository()).GetAsync(Guid.NewGuid(),CancellationToken.None);
        Assert.Equal(AuditErrors.NotFound,result.Error);
    }
    private static RecordAuditRequest CreateRequest()=>new(Guid.NewGuid(),Guid.NewGuid(),"admin",AuditActions.CreateSensor,"Sensor",Guid.NewGuid().ToString(),"Sensor created.","127.0.0.1",DateTimeOffset.UtcNow);
    private sealed class FakeRepository:IAuditLogRepository
    {
        public List<AuditLog> Items{get;}=[];
        public Task<AuditLog?> GetByIdAsync(Guid id,CancellationToken ct)=>Task.FromResult(Items.FirstOrDefault(x=>x.Id==id));
        public Task<bool> ExistsAsync(Guid id,CancellationToken ct)=>Task.FromResult(Items.Any(x=>x.Id==id));
        public Task<IReadOnlyCollection<AuditLog>> ListAsync(Guid? userId,string? action,string? resource,DateTimeOffset? from,DateTimeOffset? until,CancellationToken ct)=>Task.FromResult<IReadOnlyCollection<AuditLog>>(Items);
        public Task AddAsync(AuditLog log,CancellationToken ct){Items.Add(log);return Task.CompletedTask;}
        public Task SaveChangesAsync(CancellationToken ct)=>Task.CompletedTask;
    }
}
