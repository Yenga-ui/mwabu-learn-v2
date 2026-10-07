export interface Page<T> {
  items: T[];
  pageNumber?: number;
  page?: number;
  pageSize: number;
  hasMore?: boolean;
  totalCount?: number;
}
export interface User {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  phoneNumber?: string;
  isActive: boolean;
  lastLoginAt?: string;
  createdAt: string;
}
export interface Organisation {
  id: string;
  name: string;
  code: string;
  organisationType: string;
  parentOrganisationId?: string;
  isActive: boolean;
}
export interface Membership {
  id: string;
  userId: string;
  organisationId: string;
  isActive: boolean;
}
export interface MyMembership {
  membership: Membership;
  organisationName: string;
  organisationIsActive: boolean;
  roleCodes: string[];
}
export interface Role {
  id: string;
  code: string;
  name: string;
  grantsPlatformAuthority: boolean;
  permissions: string[];
}
export interface Access {
  platformAuthority: boolean;
  organisationId?: string;
  platformPermissions: string[];
  organisationPermissions: string[];
  cataloguePermissions: string[];
  globalPermissions: string[];
  roleCodes: string[];
}
export interface Structure {
  id: string;
  parentId: string;
  name: string;
  code?: string;
  description?: string;
  sortOrder: number;
  isActive: boolean;
}
export interface Curriculum extends Structure {
  countryCode: string;
  versions?: Structure[];
}
export interface Content {
  id: string;
  title: string;
  slug: string;
  summary?: string;
  description?: string;
  contentType: string;
  status: "Draft" | "InReview" | "Published" | "Archived";
  languageCode: string;
  sortOrder: number;
  isDownloadable: boolean;
  estimatedDurationMinutes?: number;
  publishedAt?: string;
  updatedAt?: string;
}
export interface Tag {
  id: string;
  name: string;
  slug: string;
}
export interface Collection extends Tag {
  description?: string;
  sortOrder: number;
  isActive: boolean;
}
export interface Mapping {
  nodeName?: string;
  id: string;
  nodeId: string;
  nodeType: string;
}
export interface Asset {
  id: string;
  fileName: string;
  mimeType: string;
  fileSizeBytes: number;
  assetType: string;
  checksum: string;
  isPrimary: boolean;
  viewUrl: string;
  downloadUrl: string;
}
export interface Resource {
  content: Content;
  assets: Asset[];
  tags: Tag[];
  collections: { id: string; collection: Collection; sortOrder: number }[];
  mappings: Mapping[];
}
export interface Member {
  id: string;
  userId: string;
  firstName: string;
  lastName: string;
  email?: string;
  isActive: boolean;
  roleCodes: string[];
}
export interface Project {
  id: string;
  organisationId: string;
  name: string;
  code: string;
  description?: string;
  status: string;
  startsAt?: string;
  endsAt?: string;
  curriculumVersionId?: string;
}
export interface ProjectAssignment {
  id: string;
  targetId: string;
  name: string;
}
export interface ProjectDetails {
  project: Project;
  sites: ProjectAssignment[];
  participants: ProjectAssignment[];
  resources: ProjectAssignment[];
}
export interface CurriculumAssignment {
  id: string;
  curriculumId: string;
  curriculumVersionId: string;
  name: string;
  isActive: boolean;
}
export interface LinkedLearner {
  linkId: string;
  organisationId: string;
  organisationName: string;
  learnerMembershipId: string;
  firstName: string;
  lastName: string;
}
export interface GuardianLink {
  guardianName?: string;
  learnerName?: string;
  id: string;
  guardianMembershipId: string;
  learnerMembershipId: string;
  isActive: boolean;
}
export interface Recent {
  id: string;
  title: string;
  contentType: string;
  lastOpenedAt: string;
}
export interface Device {
  id: string;
  organisationId: string;
  registeredByUserId: string;
  displayName: string;
  platform: string;
  appVersion?: string;
  isActive: boolean;
  registeredAt: string;
  lastSeenAt?: string;
  revokedAt?: string;
  credentialExpiresAt: string;
}
export interface Count {
  code: string;
  count: number;
}
export interface Report {
  organisationId?: string;
  generatedAt: string;
  organisations: number;
  schools: number;
  users: number;
  activeMemberships: number;
  projects: number;
  curricula: number;
  content: number;
  publicationStates: Count[];
  contentTypes: Count[];
  mappedContent: number;
  activeDevices: number;
  revokedDevices: number;
  syncCheckpoints: number;
}
export interface Audit {
  id: string;
  eventType: string;
  entityType?: string;
  entityId?: string;
  actorUserId?: string;
  organisationId?: string;
  occurredAt: string;
  correlationId?: string;
}
