"""
FixMyCity AI Service — Database helpers
Provides a simple connection factory for pyodbc.
All AI decisions are logged back to the DB via the .NET API (not directly).
Direct DB access here is READ-ONLY for training data and embedding retrieval.
"""
import pyodbc
import pandas as pd
from config import DB_CONN_STR


def get_connection() -> pyodbc.Connection:
    """Returns a new pyodbc connection. Caller is responsible for closing."""
    return pyodbc.connect(DB_CONN_STR)


def fetch_df(query: str, params: list = None) -> pd.DataFrame:
    """Execute a SELECT query and return a pandas DataFrame."""
    conn = get_connection()
    try:
        df = pd.read_sql(query, conn, params=params)
        return df
    finally:
        conn.close()


def execute_scalar(query: str, params: list = None):
    """Execute a query and return the first column of the first row."""
    conn = get_connection()
    try:
        cursor = conn.cursor()
        cursor.execute(query, params or [])
        row = cursor.fetchone()
        return row[0] if row else None
    finally:
        conn.close()


def fetch_all_embeddings(locality_id: int = None, category_id: int = None) -> pd.DataFrame:
    """
    Loads existing complaint embeddings for cosine similarity comparisons.
    Narrowed by locality+category to keep candidate count small (SQL filter first).
    """
    query = """
        SELECT ce.ComplaintId, ce.EmbeddingJson
        FROM dbo.ComplaintEmbeddings ce
        INNER JOIN dbo.Complaints c ON c.ComplaintId = ce.ComplaintId
        WHERE c.Status NOT IN ('Resolved','Rejected','Linked')
    """
    conditions = []
    params     = []
    if locality_id:
        conditions.append("c.LocalityId = ?")
        params.append(locality_id)
    if category_id:
        conditions.append("c.CategoryId = ?")
        params.append(category_id)

    if conditions:
        query += " AND " + " AND ".join(conditions)

    conn = get_connection()
    try:
        df = pd.read_sql(query, conn, params=params if params else None)
        return df
    finally:
        conn.close()


def load_resolved_complaints_for_training() -> pd.DataFrame:
    """
    Pulls historical resolved complaints with resolution times for LightGBM training.
    Requires at least ~200 rows before the model is meaningful.
    """
    query = """
        SELECT
            c.ComplaintId,
            c.CategoryId,
            c.Criticality,
            c.LocalityId,
            c.DeptId,
            DATEDIFF(day, c.SubmittedAt, c.ResolvedAt)               AS DaysToResolve,
            CASE WHEN EXISTS (
                SELECT 1 FROM dbo.PWGParticipationRequests p
                WHERE p.ComplaintId = c.ComplaintId AND p.Status = 'Approved'
            ) THEN 1 ELSE 0 END                                        AS HasPWG,
            ISNULL((
                SELECT SUM(co.Amount) FROM dbo.Contributions co
                WHERE co.ComplaintId = c.ComplaintId AND co.PaymentStatus = 'Success'
            ), 0)                                                       AS FundingAmount,
            CASE WHEN EXISTS (
                SELECT 1 FROM dbo.EscalationLog el WHERE el.ComplaintId = c.ComplaintId
            ) THEN 1 ELSE 0 END                                        AS WasEscalated,
            LEN(c.Description)                                          AS DescLength,
            ISNULL(cr.Stars, 0)                                         AS AvgRating,
            1                                                            AS IsResolved
        FROM dbo.Complaints c
        LEFT JOIN (
            SELECT ComplaintId, AVG(CAST(Stars AS FLOAT)) AS Stars
            FROM dbo.ComplaintRatings GROUP BY ComplaintId
        ) cr ON cr.ComplaintId = c.ComplaintId
        WHERE c.Status = 'Resolved' AND c.ResolvedAt IS NOT NULL

        UNION ALL

        SELECT
            c.ComplaintId, c.CategoryId, c.Criticality, c.LocalityId, c.DeptId,
            DATEDIFF(day, c.SubmittedAt, GETDATE()) AS DaysToResolve,
            CASE WHEN EXISTS (
                SELECT 1 FROM dbo.PWGParticipationRequests p
                WHERE p.ComplaintId = c.ComplaintId AND p.Status = 'Approved'
            ) THEN 1 ELSE 0 END,
            ISNULL((
                SELECT SUM(co.Amount) FROM dbo.Contributions co
                WHERE co.ComplaintId = c.ComplaintId AND co.PaymentStatus = 'Success'
            ), 0),
            CASE WHEN EXISTS (
                SELECT 1 FROM dbo.EscalationLog el WHERE el.ComplaintId = c.ComplaintId
            ) THEN 1 ELSE 0 END,
            LEN(c.Description),
            0,
            0
        FROM dbo.Complaints c
        WHERE c.Status NOT IN ('Resolved','Rejected','Linked')
    """
    return fetch_df(query)


def load_user_interactions_for_als() -> pd.DataFrame:
    """
    Builds the sparse user-complaint interaction matrix for ALS collaborative filtering.
    Signals: UserInterests (weight=3), PointsLedger (weight=2), ComplaintRatings (weight=Stars).
    """
    query = """
        SELECT ui.UserId, ui.CategoryId, NULL AS ComplaintId, 3.0 AS Weight
        FROM dbo.UserInterests ui WHERE ui.CategoryId IS NOT NULL

        UNION ALL

        SELECT pl.UserId, NULL, pl.RefComplaintId, 2.0
        FROM dbo.PointsLedger pl WHERE pl.RefComplaintId IS NOT NULL

        UNION ALL

        SELECT cr.CitizenUserId, NULL, cr.ComplaintId, CAST(cr.Stars AS FLOAT)
        FROM dbo.ComplaintRatings cr
    """
    return fetch_df(query)


def load_snapshot_for_prophet(category_id: int = None) -> pd.DataFrame:
    """Loads PlatformStatsSnapshot data for trend forecasting."""
    if category_id:
        query = """
            SELECT CAST(s.SnapshotDate AS DATE) AS ds,
                   sc.NewComplaints AS y
            FROM dbo.PlatformStatsSnapshot s
            INNER JOIN dbo.PlatformStatsCategorySnapshot sc
                    ON sc.SnapshotId = s.SnapshotId AND sc.CategoryId = ?
            ORDER BY s.SnapshotDate
        """
        return fetch_df(query, [category_id])
    else:
        query = """
            SELECT CAST(SnapshotDate AS DATE) AS ds, TotalComplaints AS y
            FROM dbo.PlatformStatsSnapshot ORDER BY SnapshotDate
        """
        return fetch_df(query)
