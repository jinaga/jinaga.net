# Save a Fact

When the application executes `j.Save(fact)`, several operations must take place.

- Serialize to JSON and compute hash.
- Select or Insert into `FactType` table. Gets a `FactTypeId`.
- Select or Insert into the `Fact` table. Generates a `FactId`.
- If the prior operation was an insert, for each predecessor:
  - Select or Insert into `FactType` table. Gets a `PredecessorTypeId`.
  - Select or Insert into `Role` table. Gets a `RoleId`.
  - Insert into `Edge`.
  - Insert into `Ancestor`, selecting `AncestorFactId` where `FactId` is `PredecessorFactId`, union one row with `PredecessorFactId`.

All of these operations must take place within a transaction.

## Select or Insert

The fundamental Select or Insert operation is a common pattern.
It involves selecting by alternate key, and then inserting only if no record is found.
The output of the operation is the primary key.

The Select or Insert operation also returns an indication of whether an insert actually occurred.
Some operations can be skipped if no data was inserted.
These optimizations are only possible because all operations take place within a transaction.
If the insert occurred in the past, then we can be sure that the optimized inserts also occurred.

## Insert Conflicts

Inserts are performed with the SQL modifier `INSERT OR IGNORE`.
A conflict will occur when the insert matches an existing row by alternate key.
Since we have just Selected by alternate key, this will be rare.
However, concurrency makes collisions possible.

The Select and Insert occur in the same transaction.
You might therefore conclude that a parallel operation would be guaranteed not to interfere; this is the "isolated" propery of an ACID transaction.
However, transaction isolation can only be guaranteed as strongly as locks and transaction isolation mode will allow.
If an Insert occurs, it is because the Select returned no rows.
There was therefore no row on which to place a read lock.

If an Insert is ignored, then the function `last_insert_rowid()` will not return the ID of the conflicting row.
To get that ID, you must repeat the Select.
The pattern is therefore:

```
SELECT PrimaryKey
WHERE AlternateKey=%1

if no rows returned
  INSERT OR IGNORE
    AlternateKey
    VALUES (%1)

  SELECT PrimaryKey
  WHERE AlternateKey=%1
end
```

## Fact Type and Role

Fact types are defined by the model.
They are inserted with the first fact of that type.
Most of these operations will not be inserts, as the type will have already been recorded.

As new versions of the application are deployed, new roles may be defined for a type.
We can therefore not optimize away role insertions based on the existance of the fact type.

## Edge

The Insert of an edge only occurs if the fact was just inserted.
We therefore know that the edge has not yet been inserted.
Furthermore, an edge has no primary key that will be used in a later step.
For these reasons, we can skip the Select step.

Inserting into the `Edge` table builds up the graph.
The successor of the edge is the fact that was just inserted.
The predecessor is a fact that was inserted in a previous transaction.

The `j.Save()` function will save a fact and its predecessors.
To ensure that the predecessors are written in prior transactions from their successors, each fact in the graph is written in a separate transaction.
Facts are visited in topological order, and each transaction is committed before the transaction for the next fact begins.

While traversing the graph in topological order, keep track of the IDs of each fact that was written.
Store them in a dictionary for fast retrieval by hash.
When visiting the next fact, the IDs of all of its predecessors will be in the dictionary.

## Ancestor

The `Ancestor` table is an optimization of a recursive search through the `Edge` table.
A fact will have an ancestor record for each of its predecessors, and recursively for each ancestor of each of those predecessors.

When writing a fact, you can assume that the ancestors of all of its predecessors have already been inserted.
You can therefore insert by selecting from the `Ancestor` table itself.
In the following statement, %1 is the ID of the fact just inserted, and %2 is the ID of a predecessor.

```
INSERT OR IGNORE Ancestor
  FactId, AncestorFactId
  SELECT %1, %2
  UNION SELECT %1, AncestorFactId
  FROM Ancestor
  WHERE FactId = %2
```