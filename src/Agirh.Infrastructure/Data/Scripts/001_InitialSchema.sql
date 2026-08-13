-- Initial schema for Agirh HR Database
-- Compatible with SQL Server 2025

-- Employees table
CREATE TABLE Employees (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    FirstName NVARCHAR(100) NOT NULL,
    LastName NVARCHAR(100) NOT NULL,
    Email NVARCHAR(256) NOT NULL,
    Role NVARCHAR(50) NOT NULL DEFAULT 'Collaborator',
    ManagerId UNIQUEIDENTIFIER NULL,
    LeaveBalance DECIMAL(18,2) NOT NULL DEFAULT 0,
    CompteEpargneTemps DECIMAL(18,2) NOT NULL DEFAULT 0,
    IsActive BIT NOT NULL DEFAULT 1,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_Employees_Manager FOREIGN KEY (ManagerId) REFERENCES Employees(Id),
    CONSTRAINT UQ_Employees_Email UNIQUE (Email)
);

-- LeaveRequests table
CREATE TABLE LeaveRequests (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    EmployeeId UNIQUEIDENTIFIER NOT NULL,
    StartDate DATETIME2 NOT NULL,
    EndDate DATETIME2 NOT NULL,
    Status NVARCHAR(50) NOT NULL DEFAULT 'Pending',
    Type NVARCHAR(50) NOT NULL DEFAULT 'Conges',
    DaysRequested DECIMAL(18,2) NOT NULL,
    ApprovedById UNIQUEIDENTIFIER NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_LeaveRequests_Employee FOREIGN KEY (EmployeeId) REFERENCES Employees(Id),
    CONSTRAINT FK_LeaveRequests_ApprovedBy FOREIGN KEY (ApprovedById) REFERENCES Employees(Id)
);

-- KnowledgeDocuments table with vector support
CREATE TABLE KnowledgeDocuments (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    Title NVARCHAR(500) NOT NULL,
    Content NVARCHAR(MAX) NOT NULL,
    ChunkText NVARCHAR(MAX) NOT NULL,
    -- SQL Server 2025 native VECTOR type for embeddings (1536 dims for OpenAI/text-embedding-ada-002)
    Embedding VARBINARY(MAX) NULL,
    SourceFile NVARCHAR(500) NULL,
    ChunkIndex INT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);

-- Vector index for similarity search (SQL Server 2025)
-- CREATE VECTOR INDEX IX_KnowledgeDocuments_Embedding ON KnowledgeDocuments(Embedding)
--     WITH (DIMENSION = 1536, DISTANCE = COSINE);

-- ChecklistItems table
CREATE TABLE ChecklistItems (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    Title NVARCHAR(500) NOT NULL,
    Category NVARCHAR(100) NOT NULL,
    IsRequired BIT NOT NULL DEFAULT 1,
    [Order] INT NOT NULL DEFAULT 0
);

-- Seed data
INSERT INTO ChecklistItems (Id, Title, Category, IsRequired, [Order]) VALUES
    (NEWID(), 'Valider le contrat de travail', 'Administratif', 1, 1),
    (NEWID(), 'Créer le compte email', 'IT', 1, 2),
    (NEWID(), 'Attribuer le matériel informatique', 'IT', 1, 3),
    (NEWID(), 'Planifier la visite médicale', 'RH', 1, 4),
    (NEWID(), 'Présenter l équipe', 'Management', 1, 5),
    (NEWID(), 'Organiser le parcours d onboarding', 'RH', 1, 6);
