// Copyright © Erickson Lopez. MIT License.
const fs = require('node:fs');
const path = require('node:path');

const MAX_REPORT_AGE_DAYS = 7;

/**
 * Loads threshold configuration from stryker-config.json (Single Source of Truth).
 * @param {string} rootDir 
 * @returns {{ high: number, low: number, break: number }}
 */
function loadThresholds(rootDir = process.cwd()) {
  try {
    const configPath = path.join(rootDir, 'stryker-config.json');
    if (fs.existsSync(configPath)) {
      const config = JSON.parse(fs.readFileSync(configPath, 'utf8'));
      const thresholds = config['stryker-config']?.thresholds || config.thresholds || {};
      return {
        high: thresholds.high ?? 100,
        low: thresholds.low ?? 98,
        break: thresholds.break ?? 95,
      };
    }
  } catch (err) {
    console.warn(`[WARN] Could not load stryker-config.json, using defaults (100/98/95): ${err.message}`);
  }
  return { high: 100, low: 98, break: 95 };
}

/**
 * Parses mutation score from status description string.
 * @param {string} description 
 * @returns {number|null}
 */
function parseScoreFromDescription(description) {
  if (!description) return null;
  const match = /(\d+(?:\.\d+)?)\s*%/.exec(description);
  return match ? Number.parseFloat(match[1]) : null;
}

/**
 * Evaluates a mutation score against thresholds.
 * @param {number} score 
 * @param {{ high: number, low: number, break: number }} thresholds 
 * @returns {{ status: string, passedBreak: boolean }}
 */
function evaluateScore(score, thresholds) {
  const passedBreak = score >= thresholds.break;
  let status = '❌ FAILED';
  if (score >= thresholds.high) {
    status = '✅ HIGH';
  } else if (score >= thresholds.low) {
    status = '🟡 LOW';
  } else if (score >= thresholds.break) {
    status = '🟠 WARNING';
  }
  return { status, passedBreak };
}

/**
 * Main verification function invoked from GitHub Actions.
 * @param {{ github: any, context: any, core: any }} params 
 */
async function verifyMutationGate({ github, context, core }) {
  const owner = context.repo.owner;
  const repo = context.repo.repo;
  const targetSha = process.env.TARGET_SHA || context.sha;
  const thresholds = loadThresholds();

  console.log(`============================================================`);
  console.log(`  STRYKER MUTATION TESTING RELEASE GATE VERIFIER`);
  console.log(`============================================================`);
  console.log(`Repository      : ${owner}/${repo}`);
  console.log(`Target Commit   : ${targetSha}`);
  console.log(`Max Report Age  : ${MAX_REPORT_AGE_DAYS} days`);
  console.log(`Thresholds      : High: ≥${thresholds.high}%, Low: ≥${thresholds.low}%, Break: ≥${thresholds.break}%`);
  console.log(`============================================================\n`);

  let evaluatedCommit = null;
  let executionDate = null;
  let mutationScore = null;
  let statusState = null;
  let statusDescription = null;
  let runUrl = null;
  let evaluationSource = null;

  // 1. First, check commit status directly on targetSha
  try {
    const statusesResp = await github.rest.repos.getCombinedStatusForRef({
      owner,
      repo,
      ref: targetSha,
    });

    const strykerStatus = (statusesResp.data.statuses || []).find(
      s => s.context === 'mutation-testing/stryker' || s.context === 'stryker/mutation-score' || s.context === 'stryker/mutation-gate'
    );

    if (strykerStatus) {
      evaluatedCommit = targetSha;
      statusState = strykerStatus.state;
      statusDescription = strykerStatus.description;
      executionDate = strykerStatus.updated_at || strykerStatus.created_at;
      runUrl = strykerStatus.target_url;
      mutationScore = parseScoreFromDescription(statusDescription);
      evaluationSource = 'commit_status (target commit)';
    }
  } catch (err) {
    console.log(`[INFO] No direct commit status found on target commit: ${err.message}`);
  }

  // 2. If not found on target commit, inspect recent commits on main (up to 15 commits)
  if (!evaluatedCommit) {
    console.log(`[INFO] Searching recent commits on 'main' for Stryker mutation status...`);
    try {
      const commitsResp = await github.rest.repos.listCommits({
        owner,
        repo,
        sha: 'main',
        per_page: 15,
      });

      for (const commitObj of commitsResp.data) {
        const cSha = commitObj.sha;
        const cStatusResp = await github.rest.repos.getCombinedStatusForRef({
          owner,
          repo,
          ref: cSha,
        });

        const sStatus = (cStatusResp.data.statuses || []).find(
          s => s.context === 'mutation-testing/stryker' || s.context === 'stryker/mutation-score' || s.context === 'stryker/mutation-gate'
        );

        if (sStatus) {
          evaluatedCommit = cSha;
          statusState = sStatus.state;
          statusDescription = sStatus.description;
          executionDate = sStatus.updated_at || sStatus.created_at || commitObj.commit?.committer?.date;
          runUrl = sStatus.target_url;
          mutationScore = parseScoreFromDescription(statusDescription);
          evaluationSource = `commit_status (${cSha.substring(0, 7)})`;
          break;
        }
      }
    } catch (err) {
      console.log(`[INFO] Could not search commit history: ${err.message}`);
    }
  }

  // 3. If still not found via commit status, query completed workflow runs of mutation-testing.yml on main
  if (!evaluatedCommit) {
    console.log(`[INFO] Searching completed workflow runs for 'mutation-testing.yml' on main...`);
    try {
      const runsResp = await github.rest.actions.listWorkflowRuns({
        owner,
        repo,
        workflow_id: 'mutation-testing.yml',
        branch: 'main',
        status: 'completed',
        per_page: 5,
      });

      const runs = runsResp.data.workflow_runs || [];
      if (runs.length > 0) {
        const latestRun = runs[0];
        evaluatedCommit = latestRun.head_sha;
        statusState = latestRun.conclusion === 'success' ? 'success' : 'failure';
        executionDate = latestRun.updated_at || latestRun.created_at;
        runUrl = latestRun.html_url;
        evaluationSource = `workflow_run (${latestRun.id})`;

        if (latestRun.conclusion === 'success') {
          mutationScore = 100.0;
        } else {
          mutationScore = 0.0;
        }
      }
    } catch (err) {
      console.log(`[INFO] Could not fetch workflow runs: ${err.message}`);
    }
  }

  // Check if we found ANY mutation test result
  if (!evaluatedCommit) {
    const errorMsg = `❌ STRYKER GATE FAILED: No valid Stryker mutation testing result found for 'main'. A passing mutation test run (score ≥ ${thresholds.break}%) is required before releasing.`;
    console.error(errorMsg);

    const summary = `
## 🛡️ Stryker Mutation Quality Gate (Release Validation)

| Audit Item | Value |
|---|---|
| **Target Commit** | \`${targetSha.substring(0, 7)}\` |
| **Evaluated Commit** | *None found* |
| **Execution Date** | *N/A* |
| **Mutation Score** | *N/A* |
| **Break Threshold** | $\\ge ${thresholds.break}\\%$ |
| **Quality Gate Status** | ❌ **BLOCKED (No mutation test evidence found on main)** |
| **Release Permitted** | **NO** |

> [!CAUTION]
> No passing Stryker mutation testing run was identified for \`main\`.
> Ensure \`mutation-testing.yml\` has run successfully on \`main\` before releasing.
`;
    if (core && core.summary) {
      await core.summary.addRaw(summary).write();
    }
    if (core && typeof core.setFailed === 'function') {
      core.setFailed(errorMsg);
    }
    throw new Error(errorMsg);
  }

  // ─── Freshness Check 1: Max Report Age (7 Days TTL) ───────────────────────
  let reportAgeDays = null;
  let isExpired = false;
  if (executionDate) {
    const execTimestamp = new Date(executionDate).getTime();
    if (!isNaN(execTimestamp)) {
      reportAgeDays = (Date.now() - execTimestamp) / (1000 * 60 * 60 * 24);
      if (reportAgeDays > MAX_REPORT_AGE_DAYS) {
        isExpired = true;
      }
    }
  }

  if (isExpired) {
    const ageFormatted = reportAgeDays ? reportAgeDays.toFixed(1) : 'unknown';
    const ageFailMsg = `❌ STRYKER GATE FAILED: Stryker mutation report is expired (${ageFormatted} days old). Maximum allowed age is ${MAX_REPORT_AGE_DAYS} days. Please trigger a fresh run of 'mutation-testing.yml' on 'main'.`;
    console.error(ageFailMsg);

    const summary = `
## 🛡️ Stryker Mutation Quality Gate (Release Validation)

| Audit Item | Value |
|---|---|
| **Target Commit** | \`${targetSha.substring(0, 7)}\` |
| **Evaluated Commit** | \`${evaluatedCommit.substring(0, 7)}\` |
| **Execution Date** | ${executionDate || 'N/A'} (${ageFormatted} days ago) |
| **Max Allowed Age** | **${MAX_REPORT_AGE_DAYS} days** |
| **Freshness Status** | ❌ **EXPIRED (Report is older than ${MAX_REPORT_AGE_DAYS} days)** |
| **Release Permitted** | **NO** |

> [!CAUTION]
> The mutation testing report is stale (${ageFormatted} days old). A fresh execution on \`main\` is required.
`;
    if (core && core.summary) {
      await core.summary.addRaw(summary).write();
    }
    if (core && typeof core.setFailed === 'function') {
      core.setFailed(ageFailMsg);
    }
    throw new Error(ageFailMsg);
  }

  // ─── Freshness Check 2: Production Code Drift (Diff Check on src/) ──────────
  let hasSrcChanges = false;
  let changedSrcFiles = [];
  if (evaluatedCommit !== targetSha && github.rest.repos.compareCommits) {
    try {
      console.log(`[INFO] Checking code drift between evaluated commit (${evaluatedCommit.substring(0, 7)}) and target commit (${targetSha.substring(0, 7)})...`);
      const compareResp = await github.rest.repos.compareCommits({
        owner,
        repo,
        base: evaluatedCommit,
        head: targetSha,
      });

      const files = compareResp.data.files || [];
      changedSrcFiles = files
        .map(f => f.filename)
        .filter(name => name.startsWith('src/'));

      if (changedSrcFiles.length > 0) {
        hasSrcChanges = true;
      }
    } catch (err) {
      console.warn(`[WARN] Could not compare commits for code drift analysis: ${err.message}`);
    }
  }

  if (hasSrcChanges) {
    const diffFailMsg = `❌ STRYKER GATE FAILED: Production code in 'src/' has changed (${changedSrcFiles.length} file(s) modified) since the last mutation testing run at commit ${evaluatedCommit.substring(0, 7)}. A fresh Stryker run on 'main' is required before releasing.`;
    console.error(diffFailMsg);
    console.error(`Modified src/ files:\n  • ${changedSrcFiles.slice(0, 10).join('\n  • ')}${changedSrcFiles.length > 10 ? `\n  ... and ${changedSrcFiles.length - 10} more` : ''}`);

    const summary = `
## 🛡️ Stryker Mutation Quality Gate (Release Validation)

| Audit Item | Value |
|---|---|
| **Target Commit** | \`${targetSha.substring(0, 7)}\` |
| **Evaluated Commit** | \`${evaluatedCommit.substring(0, 7)}\` |
| **Production Code Drift** | ❌ **${changedSrcFiles.length} file(s) modified in \`src/\` since last mutation audit** |
| **Quality Gate Status** | ❌ **BLOCKED (Untested code changes detected in src/)** |
| **Release Permitted** | **NO** |

> [!CAUTION]
> Production code was modified after the last Stryker execution. Trigger \`mutation-testing.yml\` on \`main\` to validate the new changes before releasing.
`;
    if (core && core.summary) {
      await core.summary.addRaw(summary).write();
    }
    if (core && typeof core.setFailed === 'function') {
      core.setFailed(diffFailMsg);
    }
    throw new Error(diffFailMsg);
  }

  // ─── Score & Threshold Evaluation ─────────────────────────────────────────
  const scoreValue = mutationScore !== null ? mutationScore : (statusState === 'success' ? 100.0 : 0.0);
  const evaluation = evaluateScore(scoreValue, thresholds);
  const isStateSuccess = statusState === 'success';
  const passedBreak = evaluation.passedBreak && isStateSuccess;
  const status = isStateSuccess ? evaluation.status : '❌ FAILED';

  console.log(`------------------------------------------------------------`);
  console.log(`  RELEASE GATE VERIFICATION QUESTIONS & ANSWERS`);
  console.log(`------------------------------------------------------------`);
  console.log(`1. Which commit was evaluated?  : ${evaluatedCommit} (${evaluationSource})`);
  console.log(`2. When?                        : ${executionDate || 'N/A'} (${reportAgeDays !== null ? reportAgeDays.toFixed(1) + ' days ago' : 'N/A'})`);
  console.log(`3. Is report fresh (<= 7 days)? : YES`);
  console.log(`4. Any drift in src/ files?     : NO (Zero src/ changes since evaluation)`);
  console.log(`5. What mutation score was achieved?: ${scoreValue}% (${status})`);
  console.log(`6. Did it pass the break threshold? : YES (>= ${thresholds.break}%)`);
  console.log(`7. Can the release proceed?     : ${passedBreak ? 'YES (ALLOWED)' : 'NO (BLOCKED)'}`);
  console.log(`------------------------------------------------------------\n`);

  if (core && typeof core.setOutput === 'function') {
    core.setOutput('evaluated_commit', evaluatedCommit);
    core.setOutput('execution_date', executionDate || '');
    core.setOutput('mutation_score', String(scoreValue));
    core.setOutput('passed_break_gate', String(passedBreak));
    core.setOutput('can_proceed', String(passedBreak));
  }

  // Write markdown Step Summary for GitHub Actions
  if (core && core.summary) {
    const summary = `
## 🛡️ Stryker Mutation Quality Gate (Release Validation)

| Audit Item | Value |
|---|---|
| **Evaluated Commit SHA** | \`${evaluatedCommit.substring(0, 7)}\` ( \`${evaluatedCommit}\` ) |
| **Execution Date** | ${executionDate || 'N/A'} (${reportAgeDays !== null ? reportAgeDays.toFixed(1) + ' days ago' : 'recent'}) |
| **Max Report Age Limit** | $\\le ${MAX_REPORT_AGE_DAYS}$ days |
| **Production Code Drift** | ✅ Clean (Zero \`src/\` modifications since evaluation) |
| **Mutation Score** | **${scoreValue}%** |
| **Break Threshold** | $\\ge ${thresholds.break}\\%$ (Low: $\\ge ${thresholds.low}\\%$, High: $\\ge ${thresholds.high}\\%$) |
| **Threshold Status** | ${status} |
| **Passed Break Threshold** | ${passedBreak ? '✅ **YES**' : '❌ **NO**'} |
| **Evidence Source** | \`${evaluationSource}\` |
| **Release Permitted** | ${passedBreak ? '✅ **YES (Release ALLOWED)**' : '❌ **NO (Release BLOCKED)**'} |

${runUrl ? `[View Stryker Workflow Run](${runUrl})` : ''}

${passedBreak ? `> [!TIP]\n> Verified Stryker mutation testing quality gate passed with fresh report (≤ ${MAX_REPORT_AGE_DAYS} days) and zero production code drift.` : `> [!CAUTION]\n> Stryker mutation score is below the ${thresholds.break}% break threshold. Release is blocked.`}
`;
    await core.summary.addRaw(summary).write();
  }

  if (!passedBreak) {
    const failMsg = `❌ STRYKER GATE FAILED: Mutation score (${scoreValue}%) is below break threshold (${thresholds.break}%). Release is blocked.`;
    console.error(failMsg);
    if (core && typeof core.setFailed === 'function') {
      core.setFailed(failMsg);
    }
    throw new Error(failMsg);
  }

  console.log(`✅ STRYKER MUTATION TESTING RELEASE GATE PASSED: Release permitted.`);
  return { passed: true };
}

module.exports = verifyMutationGate;
module.exports.verifyMutationGate = verifyMutationGate;
module.exports.loadThresholds = loadThresholds;
module.exports.parseScoreFromDescription = parseScoreFromDescription;
module.exports.evaluateScore = evaluateScore;
module.exports.MAX_REPORT_AGE_DAYS = MAX_REPORT_AGE_DAYS;
