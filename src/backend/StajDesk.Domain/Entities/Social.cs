using StajDesk.Domain.Common;

namespace StajDesk.Domain.Entities;

/* =========================================================
   Sosyal akış (kurumsal paylaşım), birebir mesajlaşma ve SSS.
   ========================================================= */

public class SocialPost : BaseEntity
{
    public int AuthorId { get; set; }
    public User Author { get; set; } = null!;
    public string Body { get; set; } = string.Empty;
    /// <summary>İsteğe bağlı görsel (depolama anahtarı; yetki denetimli uçtan sunulur)</summary>
    public string? ImageKey { get; set; }
    public string? ImageContentType { get; set; }
    /// <summary>Moderasyonla kaldırıldıysa (yazar silerse kayıt tamamen silinir)</summary>
    public DateTime? RemovedAt { get; set; }
    public int? RemovedById { get; set; }
    public string? RemovedReason { get; set; }
    public ICollection<SocialComment> Comments { get; set; } = new List<SocialComment>();
    public ICollection<SocialLike> Likes { get; set; } = new List<SocialLike>();
}

public class SocialComment : BaseEntity
{
    public int PostId { get; set; }
    public SocialPost Post { get; set; } = null!;
    public int AuthorId { get; set; }
    public User Author { get; set; } = null!;
    public string Body { get; set; } = string.Empty;
    public DateTime? RemovedAt { get; set; }
    public int? RemovedById { get; set; }
}

public class SocialLike
{
    public int PostId { get; set; }
    public int UserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Uygunsuz içerik bildirimi; moderatör kuyruğuna düşer.</summary>
public class SocialReport : BaseEntity
{
    public int PostId { get; set; }
    public SocialPost Post { get; set; } = null!;
    public int ReporterId { get; set; }
    public User Reporter { get; set; } = null!;
    public string Reason { get; set; } = string.Empty;
    public DateTime? ResolvedAt { get; set; }
    public int? ResolvedById { get; set; }
    /// <summary>removed | dismissed</summary>
    public string? Resolution { get; set; }
}

/// <summary>Birebir konuşma. Katılımcılar küçük kimlik önce olacak şekilde saklanır (tekil çift).</summary>
public class Conversation : BaseEntity
{
    public int UserAId { get; set; }
    public User UserA { get; set; } = null!;
    public int UserBId { get; set; }
    public User UserB { get; set; } = null!;
    public DateTime LastMessageAt { get; set; } = DateTime.UtcNow;
    public ICollection<DirectMessage> Messages { get; set; } = new List<DirectMessage>();
}

public class DirectMessage
{
    public long Id { get; set; }
    public int ConversationId { get; set; }
    public Conversation Conversation { get; set; } = null!;
    public int SenderId { get; set; }
    public string Body { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    /// <summary>Alıcı okuduğunda</summary>
    public DateTime? ReadAt { get; set; }
}

public class FaqItem : BaseEntity
{
    public string Category { get; set; } = string.Empty;
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsPublished { get; set; } = true;
}
