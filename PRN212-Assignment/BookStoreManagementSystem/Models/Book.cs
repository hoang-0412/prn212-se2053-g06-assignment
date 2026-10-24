using System;
using System.Collections.Generic;

namespace BookStoreManagementSystem.Models;

public partial class Book
{
    public int BookId { get; set; }

    public int CategoryId { get; set; }

    public string Title { get; set; } = null!;

    public string Author { get; set; } = null!;

    public string? Publisher { get; set; }

    public int? PublicationYear { get; set; }

    public string? Isbn { get; set; }

    public decimal Price { get; set; }

    public int StockQuantity { get; set; }

    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; }

    public string BookStatus { get; set; } = null!;

    public virtual ICollection<BookImage> BookImages { get; set; } = new List<BookImage>();

    public virtual ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();

    public virtual Category Category { get; set; } = null!;

    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}
