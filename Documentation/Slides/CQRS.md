---
marp: true
theme: default
title: CQRS
---

# CQRS

---

## What is CQRS?

- **CQRS** stands for Command Query Responsibility Segregation
- Focuses on separation of concerns between commands (writes) and queries (reads)
- Encourages vertical slicing of features for better modularity

---

## Separation of Concerns

- Commands and queries are handled by different models
- Each part of the system has a clear responsibility
- Reduces coupling and increases maintainability

---

## Vertical Slicing

- Features are implemented end-to-end, cutting across layers
- Each slice contains its own command, query, and data handling logic
- Promotes autonomy and easier testing

---

## Benefits

- Scalability
- Optimized read/write models
- Clearer code structure
- Improved modularity and maintainability

---

## Typical Architecture

- Command side: Handles writes (commands)
- Query side: Handles reads (queries)
- Event bus or message broker for communication

---

## Use Cases

- Suitable for everyday application development
- Useful in both simple and complex domains
- Helps maintain clarity and modularity regardless of project size
- Projects benefiting from clear separation and vertical feature slices

---

## Summary

- CQRS separates concerns and enables vertical slicing
- Leads to scalable, flexible, and maintainable systems
- Useful for a wide range of projects
