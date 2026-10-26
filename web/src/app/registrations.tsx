import { lazy, type ComponentType } from 'react'
import { PANELS, lazyPanel } from '@/components/hub/panel-host'
import { lazyImport } from '@/lib/lazy'
import { ItemSlots } from '@/pages/projects/slots-items'
import { ProjectSlots } from '@/pages/projects/slots'
import { ShellSlots } from './slots'
import { ADMIN_EXTRA, CREATE_SOURCES, SETTINGS_SECTIONS, copySource, holidaysRegistration, operationsRegistration, templateSettings, templateSource } from './extension-slots'

const slot = <T extends ComponentType<any>>(load: () => Promise<{ default: T }>) => lazy(() => lazyImport(load))

// All module-level registries are populated here before the router is constructed (FR-LIVE-05).
PANELS.Task = { component: lazyPanel(() => import('@/pages/projects/TaskPanel').then(m => ({ default: m.TaskPanel }))), fullPage: (id) => `/tasks/${id}` }
PANELS.Milestone = { component: lazyPanel(() => import('@/pages/projects/Milestones').then(m => ({ default: m.MilestonePanel }))) }
PANELS.Deliverable = { component: lazyPanel(() => import('@/pages/projects/Deliverables').then(m => ({ default: m.DeliverablePanel }))) }
PANELS.Decision = { component: lazyPanel(() => import('@/pages/projects/Decisions').then(m => ({ default: m.DecisionPanel }))) }
PANELS.Action = { component: lazyPanel(() => import('@/pages/projects/Meetings').then(m => ({ default: m.ActionPanel }))) }
PANELS.Risk = { component: lazyPanel(() => import('@/pages/projects/Registers').then(m => ({ default: m.RiskPanel }))) }
PANELS.Issue = { component: lazyPanel(() => import('@/pages/projects/Registers').then(m => ({ default: m.IssuePanel }))) }
PANELS.OpenRole = { component: lazyPanel(() => import('@/pages/projects/Allocations').then(m => ({ default: m.OpenRolePanel }))), flag: 'record_engine_additions' }
ItemSlots.Comments = slot(() => import('@/components/hub/comments').then(m => ({ default: m.CommentsPanel })))
ItemSlots.Links = slot(() => import('@/components/hub/links').then(m => ({ default: m.LinksPanel })))
ItemSlots.Raise = slot(() => import('@/pages/projects/Registers').then(m => ({ default: m.RaiseMenu })))
ShellSlots.Search = slot(() => import('@/components/hub/search').then(m => ({ default: m.SearchBox })))
ShellSlots.Workspace = slot(() => import('@/components/hub/workspace').then(m => ({ default: m.WorkspaceSelector })))
ShellSlots.QuickCreate = slot(() => import('@/components/hub/new-item').then(m => ({ default: m.NewItemMenu })))
ShellSlots.Bell = slot(() => import('@/components/hub/notifications').then(m => ({ default: m.BellButton })))
ProjectSlots.Health = slot(() => import('@/pages/projects/Health').then(m => ({ default: m.HealthSlot })))
ProjectSlots.Override = slot(() => import('@/pages/projects/Health').then(m => ({ default: m.OverrideAction })))
ProjectSlots.Follow = slot(() => import('@/pages/projects/Follow').then(m => ({ default: m.FollowControl })))
ADMIN_EXTRA.push(operationsRegistration, holidaysRegistration)
CREATE_SOURCES.unshift(templateSource)
CREATE_SOURCES.push(copySource)
SETTINGS_SECTIONS.push(templateSettings)
