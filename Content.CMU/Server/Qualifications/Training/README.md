# GOVFOR round training

Persistent qualifications, accreditation and checklist progress remain in the
existing QualificationService. CMUGovforTrainingSystem owns assignments and the
current topic in memory, indexed by recruit account and bound to the actual
current bodies. No assignment or skill overlay is written to the database.

The existing qualification EUI/BUI request queue carries single-recruit actions.
The server resolves the account to its current body and validates the round,
GOVFOR recruit role, synthetic exclusion, instructor accreditation, ownership,
topic/checklist permission and capacity. Only management can reassign or release
another instructor's recruit. Management cannot start lessons on their behalf.

Topics are cmuTrainingTopic prototypes linked to an enabled qualification and
checklist item. Their temporarySkills maps contain native skill prototype IDs
and levels. Add a topic in training_topics.yml and localize its name; the client
sends only its ID, never a skill list. The default capacity is four:
rucm.qualifications.max_recruits_per_instructor.

CMUTrainingSkillsComponent is a separate replicated overlay. SkillsSystem reads
max(normal role level, temporary level) through its existing GetSkill/HasSkill/
HasAllSkills/HasAnySkills APIs. Server queries revalidate ownership and authority,
so revoked access is denied immediately, even before periodic component cleanup.
Normal role skills remain intact, including changes made while a lesson runs.

CMUTrainingNavigationComponent replicates only to its owner. It contains the
current target's name and map coordinates, without pulling remote entities into
PVS. The native SquadLeaderTrackerSystem, TrackerSystem direction math, alert
category and arrow icons are reused. Recruit navigation always points to their
assigned instructor; instructor navigation points to one selected owned recruit.
The existing alert control also displays name and distance. Existing squad
tracking resumes after the training target is removed.

Explicit finish, replacement topic, checklist completion, reassignment and
release remove temporary skills. Detachment/disconnect, entity/component
termination, role or synthetic status changes, revoked instructor accreditation,
unavailable storage, disabled qualifications, removed topic/checklist permissions,
round end/restart and preset changes also clean up the relevant state. Permanent
loss of an instructor unassigns their recruits and sends a server notification.
Checks run every half second only over existing assignments; gameplay queries
deny stale access immediately. Coordinates are dirtied only when changed.

Completion of a checklist entry remains an explicit persistent action, independent
of ending a lesson. Starting/finishing a lesson never certifies a qualification.
Existing admin logs record assignments, reassignments, starts with skill lists
and every finish/removal with its reason.

Regression coverage: CMUGovforTrainingTests exercises ownership, forged topics,
native skill queries, preserving normal skills, reassignment/navigation, instructor
detachment/disconnect, synthetic exclusion, capacity and round cleanup.
