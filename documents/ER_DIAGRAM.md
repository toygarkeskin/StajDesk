# StajDesk - ER Diyagramı

```mermaid
erDiagram
    USERS ||--o{ TICKETS : creates
    CATEGORIES ||--o{ TICKETS : contains
    TICKETS ||--o{ COMMENTS : has
    USERS ||--o{ COMMENTS : writes
    USERS ||--o{ NOTIFICATIONS : receives
    TICKETS o|--o{ NOTIFICATIONS : relates_to
```