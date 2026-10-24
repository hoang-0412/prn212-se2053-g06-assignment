using System;
using System.Collections.Generic;

namespace BookStoreManagementSystem.Models;

public partial class BookImage
{
    public int ImageId { get; set; }

    public int BookId { get; set; }

    public byte[] ImageData { get; set; } = null!;

    public string MimeType { get; set; } = null!;

    public bool IsCover { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Book Book { get; set; } = null!;
}
