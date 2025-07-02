# File Naming Convention

## Logic

YYYYMMDD_HHMM-TYPE-description_in_lowercase.md

### Types

- FEAT: New feature implementation
- FIX: Bug fix or error resolution
- CHORE: Routine task or maintenance
- REFACTOR: Codebase refactoring

## Examples

### Feature Implementation
20250524_0041-FEAT-route_standardization_product_customization.md
20250525_1816-FIX-typescript_cart_errors_comprehensive_fix.md
20250525_1832-CHORE-layout_diagnostic_cleanup.md

### Functionality Documentation
20250215_2301-properties_panel_zustand_implementation.md
20250216_0010-state_management_architecture.md

---

# Template

[Component/Feature Name]
[Date], [Time]
e.g.,
"Properties Panel Zustand Implementation
16 February 2025, 00:10"

## Overview

[Brief description focusing on either:
1. For Features (FEAT/FIX): What problem this solves, why it was needed
2. For Functionality (FUNC): How a particular system/component works, its architecture]

---

## Changes Made

1. [Major Component/Area 1]
- Key implementations
- State changes
- Architecture decisions
```typescript
// Example implementation
interface ComponentState {
    property: string;
    // ... other properties
}
```

2. [Major Component/Area 2]
- Implementation details
- Integration points
- Dependencies affected

3. [Major Component/Area 3]
- Technical changes
- Performance considerations
- Type safety improvements

---

## Technical Details

### Architecture/Implementation

1. Core Structure:
```typescript
// Core interfaces, types, or classes
interface MainInterface {
    // ... properties
}
```

2. Key Integrations:
- How components interact
- Data flow
- State management

3. Important Workflows:
- Step-by-step processes
- State transitions
- Error handling

---

## Testing/Validation

1. Verified Functionality:
- List of tested features
- State transitions verified
- Edge cases covered

2. Console Output (if relevant):
```
[Relevant console logs]
```

3. Performance Metrics (if applicable):
- Load times
- Memory usage
- Render performance

---

## Future Considerations

1. Immediate TODOs:
   - Critical improvements needed
   - Known limitations
   - Pending features

2. Long-term Improvements:
   - Scalability considerations
   - Future integrations
   - Performance optimizations

---

## Dependencies

- Framework: ^version
- Library1: ^version
- Library2: ^version

[Include versions for tracking and compatibility]

---

## Related Documentation

- Link to API docs
- Link to architecture docs
- Link to related components
- Updated documentation files

---

## Notes

[Any additional information that doesn't fit above:
- Special considerations
- Important warnings
- Configuration requirements
- Environment dependencies]

---

# Updating Existing Development Logs

When updating an existing development log, maintain a unified document rather than creating separate log entries. Follow these guidelines:

1. **Update the Title and Date**:
   - Modify the title to reflect the full scope of work (original + updates)
   - Update the date/time to the latest modification

2. **Consolidate the Overview**:
   - Expand the overview to include both original and new work
   - Present a cohesive narrative that covers the entire scope

3. **Integrate New Changes**:
   - Merge new changes into the existing "Changes Made" section
   - Group related changes under appropriate headings
   - Avoid creating separate "Update" sections

4. **Unify Technical Details**:
   - Organize technical details under clear subsections
   - Ensure code examples from both original and new work are included
   - Maintain a logical flow between different implementation aspects

5. **Combine Testing/Validation**:
   - Present all testing information in a unified structure
   - Group related test results under appropriate categories

6. **Consolidate Future Considerations**:
   - Merge and reorganize future considerations
   - Remove items that have been completed
   - Add new considerations based on the latest work

7. **Update the Critical Note**:
   - Ensure the handoff note reflects the comprehensive nature of the document
   - Highlight both original and new aspects of the work

This unified approach creates a more readable, comprehensive document that provides a complete picture of the feature or component's development history.

---

# 3-Point Public Summary

> This section provides a user-friendly explanation of technical changes that can be shared with non-technical stakeholders or end users.

**🛋️ [Benefit 1 - Start with an emoji]**
Simple explanation of the first key benefit in plain language
→ How this helps users in practical terms

**📏 [Benefit 2 - Start with an emoji]**
Simple explanation of the second key benefit without technical jargon
→ Real-world impact for everyday users

**🎮 [Benefit 3 - Start with an emoji]**
Simple explanation of the third key benefit using familiar concepts
→ Practical example or comparison to something users already understand

---

# Critical Note for Handoff

Intended for SOTA LLM analysis: This development log should be structured in a way that it can be easily handed off to advanced language models like o1-pro or other state-of-the-art reasoning systems. It should include enough context, detail, and clarity to enable the model to:

Understand the changes made and why they were necessary.

Resolve any potential issues or errors if they arise.

Be able to follow up on next steps and assess the benefits effectively.

Be used for debugging, analysis, or further iteration without additional clarification from the original developer.