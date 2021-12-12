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

## Caching

Select or Insert operations are safe to cache.
A cache is a dictionary from alternate key to primary key.
Entries already in the cache will never be invalidated.
The primary keys will not change.

The caches for the `FactType` and `Role` tables can be maintained for the duration of the application.
Most operations against these caches will be hits.
The number of elements in the cache will cease to grow early in the application lifecycle, after at least one instance of each fact type in the data model has been written.
The benefits of maintaining this cache outweigh the cost of synchronizing it across threads, especially if structural sharing is employed for immutable lock-free reads.

The cache for `Fact` will be most valueable for the duration of the `j.Save()` operation.
This cache will necessarily be part of the topoligical sort algorithm.
But beyond the scope of the fact graph, there is less value in maintaining this cache.
Most save operations will result in new facts, and so the leaves of the traversal will result in cache misses.
And the cache will continue to grow as the application runs.

A more complicated cache could provide some benefit beyond the topological sort of a given graph.
It would need to employ a strategy to age out entries with a low probability of cache hits in the future.
If the aging strategy takes depth into account, it could recognize that there is more value in retaining top-level facts than leaf facts.
Such an algorithm would be complex, exert pressure on garbage collection, and require thread synchronization.
It might not provide enough value to justify those costs.