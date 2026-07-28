# Jenkins Pipeline Source Fix Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Ensure the OpenShift Jenkins pipeline builds the repository defined by BuildConfig source instead of a hardcoded external repository.

**Architecture:** Replace the hardcoded Jenkins `git` checkout with `checkout scm` so Jenkins uses SCM metadata from the OpenShift pipeline BuildConfig (`spec.source.git.uri` and `ref`). Keep build and binary build stages unchanged. Verify by triggering pipeline and confirming route output matches this repo’s `gremlins/src/App.js`.

**Tech Stack:** Jenkins Pipeline (Groovy), OpenShift BuildConfig/Build/ImageStream/DeploymentConfig, npm/react-scripts, oc CLI

---

### Task 1: Update Jenkins Checkout Source

**Files:**
- Modify: `jenkins/Jenkinsfile:10-13`
- Test: Runtime verification via `oc start-build` and route content checks

**Step 1: Write the failing verification check**

Run:
```bash
curl -k -sS https://gremlins-devops.apps.okd.dorneean.wan/static/js/main*.chunk.js
```

Expected: Output contains old app text (for example, `Learn React now ! BLA`) that does not exist in `gremlins/src/App.js`.

**Step 2: Run source trace to confirm mismatch origin**

Run:
```bash
sed -n '1,40p' jenkins/Jenkinsfile
```

Expected: Checkout stage contains hardcoded external repo URL.

**Step 3: Write minimal implementation**

Change checkout stage from:
```groovy
git 'https://github.com/adynetro/openshift-demo-pipeline.git'
```

to:
```groovy
checkout scm
```

**Step 4: Run verification build**

Run:
```bash
oc start-build bc/gremlins-pipeline -o name
```

Expected: New pipeline build starts successfully.

**Step 5: Verify deployed content**

Run:
```bash
curl -k -sS https://gremlins-devops.apps.okd.dorneean.wan | head -n 40
curl -k -sS https://gremlins-devops.apps.okd.dorneean.wan/static/js/main*.chunk.js | grep -E "The Gremlins|Pipeline Gremlin|Hosted on OpenShift"
```

Expected: Route content matches current `gremlins/src/App.js` text and no longer serves old app strings.

**Step 6: Commit**

```bash
git add jenkins/Jenkinsfile
git commit -m "Fix Jenkins pipeline checkout to use BuildConfig SCM source"
```

### Task 2: Push and Confirm Pipeline Status

**Files:**
- Modify: none
- Test: OpenShift build status and rollout artifacts

**Step 1: Push change**

Run:
```bash
git push origin master
```

Expected: Push succeeds.

**Step 2: Confirm pipeline + image handoff**

Run:
```bash
oc get builds --sort-by=.metadata.creationTimestamp | tail -n 5
oc get is gremlins -n devops
oc get dc gremlins -n devops
```

Expected: Pipeline/build chain reflects new run and deployment config is tied to latest imagestream tag.

**Step 3: Commit plan doc**

```bash
git add docs/plans/2026-07-27-jenkins-pipeline-source-fix.md
git commit -m "Add implementation plan for Jenkins pipeline source fix"
```
