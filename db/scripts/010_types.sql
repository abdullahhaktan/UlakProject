/* =====================================================================
   010_types.sql  --  Table-valued parameter types.
   Used by usp_Proof_Create to insert the photo rows in one set-based
   statement alongside the proof header.
   ===================================================================== */
GO

IF TYPE_ID(N'dbo.ProofPhotoType') IS NULL
BEGIN
    CREATE TYPE dbo.ProofPhotoType AS TABLE
    (
        Url        NVARCHAR(400) NOT NULL,
        OrderIndex INT           NOT NULL
    );
END
GO
