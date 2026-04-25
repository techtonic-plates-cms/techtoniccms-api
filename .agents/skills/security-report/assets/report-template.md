# TechtonicCMS Security Audit Report

**Date:** {{date}}
**Scope:** {{scope}}
**Auditor:** security-report agent

---

## Executive Summary

| Metric | Value |
|--------|-------|
| Risk Score | {{risk_score}} |
| Critical | {{critical_count}} |
| High | {{high_count}} |
| Medium | {{medium_count}} |
| Low | {{low_count}} |
| Total Findings | {{total_count}} |

{{executive_summary_text}}

---

## Methodology

This audit was performed by a dedicated security subagent using tool-driven codebase analysis. The following phases were executed:

1. **Reconnaissance** — Mapped middleware, DI services, and configuration files
2. **Authentication & Sessions** — Reviewed JWT, session, password, and API key handling
3. **ABAC Authorization** — Analyzed policy evaluation, caching, and audit logging
4. **Data Protection & GraphQL** — Inspected asset handling, security headers, and GraphQL configuration
5. **Infrastructure** — Evaluated Docker Compose, environment variables, and network exposure

---

## Findings

### Critical

{{#each critical_findings}}
#### {{title}}

- **Category:** {{category}}
- **Location:** `{{location}}`
- **Effort:** {{effort}}

**Evidence:**
```{{language}}
{{evidence}}
```

**Impact:** {{impact}}

**Recommendation:** {{recommendation}}

---
{{/each}}

### High

{{#each high_findings}}
#### {{title}}

- **Category:** {{category}}
- **Location:** `{{location}}`
- **Effort:** {{effort}}

**Evidence:**
```{{language}}
{{evidence}}
```

**Impact:** {{impact}}

**Recommendation:** {{recommendation}}

---
{{/each}}

### Medium

{{#each medium_findings}}
#### {{title}}

- **Category:** {{category}}
- **Location:** `{{location}}`
- **Effort:** {{effort}}

**Evidence:**
```{{language}}
{{evidence}}
```

**Impact:** {{impact}}

**Recommendation:** {{recommendation}}

---
{{/each}}

### Low

{{#each low_findings}}
#### {{title}}

- **Category:** {{category}}
- **Location:** `{{location}}`
- **Effort:** {{effort}}

**Evidence:**
```{{language}}
{{evidence}}
```

**Impact:** {{impact}}

**Recommendation:** {{recommendation}}

---
{{/each}}

## Recommendations (Prioritized)

{{#each recommendations}}
{{@index}}. **{{title}}** ({{effort}}) — {{summary}}
{{/each}}

---

## Appendix

### Scanned Files

{{#each scanned_files}}
- `{{this}}`
{{/each}}

### Policy Coverage Matrix

{{policy_coverage_matrix}}
