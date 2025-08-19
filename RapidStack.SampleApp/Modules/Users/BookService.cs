using RapidStack.AutoDI;
using RapidStack.AutoEndpoint.Attributes;

namespace RapidStack.SampleApp.Modules.Users;


// DTOs
public record BookDto(Guid Id, string Title, string Author, DateTime PublishedDate);
public record CreateBookDto(string Title, string Author, DateTime PublishedDate);
public record UpdateBookDto(string Title, string Author, DateTime PublishedDate);
public class BookQueryParam
{
    public string Title { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? Level { get; set; }
}
// Service
[AutoEndpoint("api/books")]
[Injectable(ServiceLifetime.Scoped)]
public class BookService
{
    public BookDto Get(Guid id, BookQueryParam queryParam)
    {
        return new BookDto(id, "Sample Book", "John Doe", DateTime.UtcNow);
    }

    public List<BookDto> GetList() => new()
    {
        new BookDto(Guid.NewGuid(), "Book 1", "Author 1", DateTime.UtcNow),
        new BookDto(Guid.NewGuid(), "Book 2", "Author 2", DateTime.UtcNow)
    };

    public void Create(CreateBookDto input)
    {
        // Save logic here
    }

    public void Update(Guid id, UpdateBookDto input)
    {
        // Update logic here
    }

    public void Delete(Guid id)
    {
        // Delete logic here
    }
}
