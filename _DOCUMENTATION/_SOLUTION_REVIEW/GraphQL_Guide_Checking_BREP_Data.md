# GraphQL Guide for Checking BREP Data in Speckle

*Created: 3 July 2025*

## Overview

This guide provides GraphQL queries to inspect Speckle data and verify BREP geometry transfer. These queries are essential for debugging geometry conversions and confirming that BREP data is properly sent from connectors.

---

## Prerequisites

1. **GraphQL Endpoint**: https://app.speckle.systems/graphql
2. **Required Information**:
   - Project/Stream ID (from your Speckle URL)
   - Model ID (optional, for project-based queries)
   - Commit/Version ID (for specific commits)
3. **Authentication**: Must be logged into Speckle

---

## Step 1: Extract IDs from Speckle URL

Given a URL like: `https://app.speckle.systems/projects/d700651b58/models/9de6eef31d`

- **Project ID**: `d700651b58`
- **Model ID**: `9de6eef31d`

---

## Step 2: Find Commit Information

### Query to List All Commits/Versions

```graphql
query GetCommits {
  project(id: "d700651b58") {
    model(id: "9de6eef31d") {
      versions(limit: 10) {
        items {
          id
          message
          createdAt
          referencedObject
        }
      }
    }
  }
}
```

**Response includes**:
- `id`: The commit ID
- `message`: Commit message (if provided)
- `createdAt`: Timestamp
- `referencedObject`: The root object ID for this commit

---

## Step 3: Explore Commit Structure

### Query to See Collection Structure

```graphql
query ExploreCommitStructure {
  stream(id: "d700651b58") {
    object(id: "YOUR_REFERENCED_OBJECT_ID") {
      totalChildrenCount
      data
      children(limit: 50, depth: 2) {
        objects {
          id
          speckleType
          data
        }
      }
    }
  }
}
```

This shows the hierarchical structure of your commit, typically:
- Root Collection
  - Level Collections
    - Category Collections (Walls, Floors, etc.)
      - Individual Elements

---

## Step 4: Find Actual Geometry Elements

### Query to Drill Down to Elements

Since Revit organizes data hierarchically, you need to traverse the tree:

```graphql
query FindWallElements {
  stream(id: "d700651b58") {
    object(id: "86a6021ed48214eec9cd2680d5f71eee") {
      data
      children(limit: 100, depth: 3) {
        objects {
          id
          speckleType
          data
        }
      }
    }
  }
}
```

Replace the object ID with IDs found in previous queries to navigate deeper.

---

## Step 5: Check for BREP Data

### Query to Examine DisplayValue

Once you find an actual element (like a Wall), check its displayValue:

```graphql
query CheckElementDisplayValue {
  stream(id: "d700651b58") {
    object(id: "ELEMENT_ID_HERE") {
      data
    }
  }
}
```

Look for this pattern in the response:

```json
"displayValue": [
  {
    "speckle_type": "Objects.Geometry.Brep",
    "Faces": [...],
    "Edges": [...],
    "Vertices": [...],
    "IsClosed": true
  },
  {
    "speckle_type": "Objects.Geometry.Mesh",
    "vertices": [...],
    "faces": [...]
  }
]
```

---

## Step 6: Quick Type Check

### Query to List All Geometry Types

To quickly see what geometry types are in your commit:

```graphql
query ListGeometryTypes {
  stream(id: "d700651b58") {
    object(id: "YOUR_OBJECT_ID") {
      children(limit: 1000, depth: 10) {
        objects {
          speckleType
        }
      }
    }
  }
}
```

Search the response for:
- `"speckleType": "Objects.Geometry.Brep"`
- `"speckleType": "Objects.Geometry.Mesh"`
- `"speckleType": "Objects.Data.RevitObject"`

---

## Helpful Tips

### 1. Finding Elements Faster

Use the search functionality in GraphQL response:
- Ctrl+F (or Cmd+F on Mac)
- Search for: `"Walls"`, `"category": "Walls"`, or specific element names

### 2. Understanding Speckle References

When you see:
```json
{
  "referencedId": "abc123...",
  "speckle_type": "reference"
}
```
This means the actual data is stored separately. Use that ID in a new query.

### 3. Common Element Path

Typical Revit element path:
1. Root Collection
2. Model Collection (with project name)
3. Level Collection (e.g., "Level 1")
4. Category Collection (e.g., "Walls")
5. Individual Elements (with displayValue containing geometry)

### 4. Debugging BREP Issues

If BREP is missing:
1. Check if elements have `displayValue` arrays
2. Verify `displayValue` contains objects with `speckle_type: "Objects.Geometry.Brep"`
3. If only Mesh objects exist, BREP conversion may have failed
4. Check element properties for any BREP-related flags

---

## Example: Complete Flow

1. **Get commits**:
```graphql
query { 
  project(id: "d700651b58") { 
    model(id: "9de6eef31d") { 
      versions(limit: 1) { 
        items { id referencedObject } 
      } 
    } 
  } 
}
```

2. **Navigate to elements** (using IDs from previous responses):
```graphql
query {
  stream(id: "d700651b58") {
    object(id: "REFERENCED_OBJECT_ID") {
      children(limit: 10, depth: 1) {
        objects {
          id
          speckleType
          data
        }
      }
    }
  }
}
```

3. **Check specific element**:
```graphql
query {
  stream(id: "d700651b58") {
    object(id: "WALL_ELEMENT_ID") {
      data
    }
  }
}
```

---

## Common Issues and Solutions

### Issue: Can't find elements
**Solution**: Increase depth and limit in children query:
```graphql
children(limit: 1000, depth: 5)
```

### Issue: Too much data
**Solution**: Query specific fields:
```graphql
query {
  stream(id: "d700651b58") {
    object(id: "ID") {
      children(limit: 100) {
        objects {
          speckleType
        }
      }
    }
  }
}
```

### Issue: BREP not visible
**Solution**: Check if the commit was made after BREP implementation. Older commits won't have BREP data.

---

## Real Example: Debugging BREP Transfer

### Example Session (3 July 2025)

1. **Found wall element with displayValue reference**:
```json
{
  "name": "Walls - GENERIC-500mm",
  "displayValue": [
    {
      "referencedId": "d9295be0aba9ab1a0d9c62d3a34a2b46",
      "speckle_type": "reference"
    }
  ]
}
```

2. **Checked the referenced geometry**:
```graphql
query {
  stream(id: "d700651b58") {
    object(id: "d9295be0aba9ab1a0d9c62d3a34a2b46") {
      data
    }
  }
}
```

3. **Result showed only Mesh, no BREP**:
```json
{
  "speckle_type": "Objects.Geometry.Mesh",
  "vertices": [...],
  "faces": [...],
  "area": 0,
  "volume": 0
}
```

This indicates BREP conversion is not working - only mesh data is present.

## Verification Checklist

- [ ] Found commit ID and referenced object
- [ ] Located individual elements (not just collections)
- [ ] Checked displayValue array exists
- [ ] Confirmed presence of `Objects.Geometry.Brep` in displayValue
- [ ] Verified BREP has expected properties (Faces, Edges, Vertices)
- [ ] Confirmed Mesh fallback also exists

## Expected BREP Structure

When BREP is working correctly, you should see both objects in an array:
```json
[
  {
    "speckle_type": "Objects.Geometry.Brep",
    "Faces": [...],
    "Edges": [...],
    "Vertices": [...],
    "Surfaces": [...],
    "IsClosed": true
  },
  {
    "speckle_type": "Objects.Geometry.Mesh",
    "vertices": [...],
    "faces": [...]
  }
]
```

---

## Notes

- GraphQL queries are case-sensitive
- The GraphQL playground has auto-complete - use it!
- You can save queries in the playground for reuse
- Large queries may timeout - reduce depth/limit if needed
- The `__closure` field helps with object deduplication but can be ignored for inspection