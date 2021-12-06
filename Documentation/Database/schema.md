# Database Schema

The Jinaga database is composed of 7 tables.
Two are simple lookups that give a primary key to domain-specified names.

## FactType

| Column     | Type        | Description                                     |
| ---------- | ----------- | ----------------------------------------------- |
| FactTypeId | int         | Primary key representing the fact type locally. |
| Name       | varchar(50) | The name given to the fact type.                |

The name is an alternate key.

## Role

| Column            | Type        | Description                                                 |
| ----------------- | ----------- | ----------------------------------------------------------- |
| RoleId            | int         | Primary key representing the role locally.                  |
| DefiningTypeId    | int         | ID of the type that defines the role -- the successor type. |
| Name              | varchar(20) | The name given to the role.                                 |
| PredecessorTypeId | int         | ID of the referenced type -- the predecessor type.          |

The DefiningTypeId and PredecessorTypeId are foreign keys to the FactType table.
DefiningTypeId, Name, and PredecessorTypeId combine to form the alternate key.

The next two tables represent the graph of facts.
They are the primary source for pipeline queries.

## Fact

| Column       | Type         | Decription                                                   |
| ------------ | ------------ | ------------------------------------------------------------ |
| FactId       | int          | Primary key representing the fact locally.                   |
| FactTypeId   | int          | ID of the fact's type.                                       |
| Hash         | varchar(100) | The Base-64 encoded SHA-512 hash of the canonicallized fact. |
| Fields       | JSONB        | A JSON object containing all of the fields of the fact.      |
| Predecessors | JSONB        | A JSON object containing the predecessor hashes.             |
| DateLearned  | DateTime     | The moment that the fact was inserted at the local node.     |

The FactTypeId is a foreign key to the FactType table.
The alternate key is composed of FactTypeId and Hash.

## Edge

| Column        | Type | Description                                  |
| ------------- | ---- | -------------------------------------------- |
| RoleId        | int  | ID of the role, as defined by the successor. |
| SuccessorId   | int  | ID of the successor fact.                    |
| PredecessorId | int  | ID of the predecessor fact.                  |

All of the columns are foreign keys.
The tuple of all three are constrained to be unique.

The last two tables make up the keystore.
In future deployments, these will be separated from the rest.

## Ancestor

| Column         | Type | Description                                  |
| -------------- | ---- | -------------------------------------------- |
| FactId         | int  | ID of a fact.                                |
| AncestorFactId | int  | ID of a direct or indirect predecessor fact. |

The ancestor table is an optimization.
It is a derivative of the Edge table for computing the transitive closure.
Both FactId and AncestorFactId are foreign keys to the Fact table.
The alternate key is the pair of them.

## User

| Column         | Type          | Description                           |
| -------------- | ------------- | ------------------------------------- |
| UserId         | int           | Primary key of the user.              |
| Provider       | varchar(100)  | Identifier of the identity provider.  |
| UserIdentifier | varchar(50)   | Provider-specified user identifier.   |
| PrivateKey     | varchar(1800) | Base-64 encoded RSA-2048 private key. |
| PublicKey      | varchar(500)  | Base-64 encoded RSA-2048 publc key.   |

The Provider and UserIdentifier form one alternate key.
The PublicKey forms another.

## Signature

| Column      | Type         | Description                                                   |
| ----------- | ------------ | ------------------------------------------------------------- |
| FactTypeId  | int          | ID of the fact's type.                                        |
| Hash        | varchar(100) | The Base-64 encoded SHA-512 hash of the canonicallized fact.  |
| UserId      | int          | ID of the user who signed the fact.                           |
| Signature   | varchar(400) | The base-64 encoded HMACSHA signature of the fact.            |
| DateLearned | DateTime     | The moment that the signature was inserted at the local node. |

The FactTypeId is a foreign key to the FactType table.
When the keystore is separated from the fact store, both get a FactType table.
The UserId is a foreign key to the User table.
The alternate key is the tuple FactTypeId, Hash, and UserId.