using AspireDeezNuts.Shared.Models;

namespace AspireDeezNuts.ApiService.Tests.Builders;

/// <summary>
/// Builder Pattern: Constructs Post objects step by step for testing
/// Provides a fluent interface for creating test data with sensible defaults
/// </summary>
public class PostBuilder
{
    private long _id = 1;
    private int _userId = 1;
    private string _title = "Default Test Post";
    private string _body = "Default test post body content";

    public PostBuilder WithId(long id)
    {
        _id = id;
        return this;
    }

    public PostBuilder WithUserId(int userId)
    {
        _userId = userId;
        return this;
    }

    public PostBuilder WithTitle(string title)
    {
        _title = title ?? throw new ArgumentNullException(nameof(title));
        return this;
    }

    public PostBuilder WithBody(string body)
    {
        _body = body ?? throw new ArgumentNullException(nameof(body));
        return this;
    }

    public Post Build()
    {
        return new Post(_id, _userId, _title, _body);
    }

    public static PostBuilder Create() => new();

    public static PostBuilder CreateForUser(int userId)
    {
        return new PostBuilder().WithUserId(userId);
    }

    public static PostBuilder CreateWithTitle(string title)
    {
        return new PostBuilder().WithTitle(title);
    }

    public PostBuilder Reset()
    {
        _id = 1;
        _userId = 1;
        _title = "Default Test Post";
        _body = "Default test post body content";
        return this;
    }
}

/// <summary>
/// Collection builder for creating multiple posts efficiently
/// </summary>
public class PostCollectionBuilder
{
    private readonly List<Post> _posts = [];
    private int _nextId = 1;

    public PostCollectionBuilder AddPost(Action<PostBuilder> configure)
    {
        var builder = new PostBuilder().WithId(_nextId++);
        configure(builder);
        _posts.Add(builder.Build());
        return this;
    }

    public PostCollectionBuilder AddPost(Post post)
    {
        _posts.Add(post);
        _nextId = Math.Max(_nextId, (int)post.Id + 1);
        return this;
    }

    public PostCollectionBuilder AddPostsForUser(int userId, int count, string titlePrefix = "Post")
    {
        for (int i = 1; i <= count; i++)
        {
            AddPost(builder => builder
                .WithUserId(userId)
                .WithTitle($"{titlePrefix} {i}")
                .WithBody($"Body content for {titlePrefix} {i}"));
        }
        return this;
    }

    public List<Post> Build()
    {
        return [.. _posts];
    }

    public static PostCollectionBuilder Create() => new();

    public PostCollectionBuilder Clear()
    {
        _posts.Clear();
        _nextId = 1;
        return this;
    }
}