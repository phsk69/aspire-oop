using AspireDeezNuts.Shared.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace AspireDeezNuts.ApiService.Tests.Factories;

/// <summary>
/// Abstract Factory Pattern: Creates families of related repository objects
/// Allows switching between different repository implementations for testing vs production
/// </summary>
public interface IRepositoryFactory
{
    IPostRepository CreatePostRepository();
}

/// <summary>
/// Concrete factory for production repositories (external APIs)
/// </summary>
public class ProductionRepositoryFactory(IServiceProvider serviceProvider) : IRepositoryFactory
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;

    public IPostRepository CreatePostRepository()
    {
        return _serviceProvider.GetRequiredService<IPostRepository>();
    }
}

/// <summary>
/// Concrete factory for test repositories (in-memory implementations)
/// </summary>
public class TestRepositoryFactory : IRepositoryFactory
{
    private readonly IPostRepository? _postRepository;

    public TestRepositoryFactory()
    {
        _postRepository = null; // Will create new instance each time
    }

    public TestRepositoryFactory(IPostRepository postRepository)
    {
        _postRepository = postRepository;
    }

    public IPostRepository CreatePostRepository()
    {
        return _postRepository ?? new Repositories.InMemoryPostRepository();
    }
}

/// <summary>
/// Factory for creating repositories with pre-seeded test data
/// </summary>
public class SeededTestRepositoryFactory : IRepositoryFactory
{
    private readonly Repositories.InMemoryPostRepository _repository;

    public SeededTestRepositoryFactory()
    {
        _repository = new Repositories.InMemoryPostRepository();
    }

    public SeededTestRepositoryFactory(IEnumerable<AspireDeezNuts.Shared.Models.Post> testData)
    {
        _repository = new Repositories.InMemoryPostRepository(testData);
    }

    public IPostRepository CreatePostRepository()
    {
        return _repository;
    }
}