// Mirrors the backend DTOs. Enums are serialized as strings by the API.

export type Difficulty = 'Easy' | 'Moderate' | 'Hard' | 'Expert';
export type PointOfInterestType = 'WaterSource' | 'Shelter' | 'Campsite' | 'Viewpoint' | 'Other';
export type TrailCondition = 'Good' | 'Muddy' | 'Snow' | 'Ice' | 'Flooded' | 'Overgrown' | 'Blocked' | 'Closed';
export type TrailSort = 'Relevance' | 'Distance' | 'Rating' | 'Newest' | 'Length';

export type Paged<T> = { items: T[]; page: number; pageSize: number; totalCount: number; totalPages: number };
export type LatLng = { latitude: number; longitude: number };
export type Bounds = { minLatitude: number; minLongitude: number; maxLatitude: number; maxLongitude: number };

export type TrailSummary = {
  id: string;
  name: string;
  region: string;
  difficulty: Difficulty;
  distanceMeters: number;
  elevationGainMeters: number;
  estimatedDurationMinutes: number;
  isLoop: boolean;
  start: LatLng;
  averageRating: number;
  reviewCount: number;
  distanceFromSearchPointMeters: number | null;
};

export type PointOfInterest = {
  id: string;
  name: string;
  type: PointOfInterestType;
  latitude: number;
  longitude: number;
  elevation: number | null;
  description: string | null;
};

export type ConditionReport = { id: string; condition: TrailCondition; note: string | null; userId: string; reportedAtUtc: string };

export type TrailDetails = TrailSummary & {
  description: string | null;
  elevationLossMeters: number;
  maxElevationMeters: number | null;
  bounds: Bounds;
  isFavorite: boolean;
  createdBy: string;
  createdAtUtc: string;
  pointsOfInterest: PointOfInterest[];
  photos: { url: string; caption: string | null; uploadedAtUtc: string }[];
  latestCondition: ConditionReport | null;
};

export type RoutePoint = { latitude: number; longitude: number; elevation: number | null };
export type TrailRoute = { trailId: string; totalPoints: number; points: RoutePoint[] };
export type ElevationSample = { distanceMeters: number; elevationMeters: number };
export type Review = { id: string; userId: string; rating: number; comment: string | null; createdAtUtc: string; updatedAtUtc: string | null };

export type TrackPoint = { latitude: number; longitude: number; elevation: number | null; timestampUtc: string };

export type ActivitySummary = {
  id: string;
  name: string;
  trailId: string | null;
  source: 'Recorded' | 'ImportedGpx';
  startedAtUtc: string;
  endedAtUtc: string;
  distanceMeters: number;
  elevationGainMeters: number;
  movingDurationSeconds: number;
  averageSpeedMetersPerSecond: number;
};

export type ActivityDetails = ActivitySummary & {
  notes: string | null;
  elevationLossMeters: number;
  maxElevationMeters: number | null;
  elapsedDurationSeconds: number;
  bounds: Bounds;
  segments: TrackPoint[][];
  /** The planned route the hike followed, if it was started from one. */
  routeId: string | null;
  elevationProfile: ElevationSample[];
};

export type ActivityTotals = {
  hikes: number;
  distanceMeters: number;
  elevationGainMeters: number;
  movingDurationSeconds: number;
  longestHikeMeters: number;
  highestElevationMeters: number | null;
};

export type Statistics = {
  allTime: ActivityTotals;
  thisYear: ActivityTotals;
  lastTwelveMonths: { year: number; month: number; hikes: number; distanceMeters: number; elevationGainMeters: number }[];
};

export type Units = 'Metric' | 'Imperial';
export type MapStyle = 'Outdoors' | 'Satellite' | 'Terrain';
export type Preferences = { units: Units; mapStyle: MapStyle; shareLocationWithGroups: boolean; notificationsEnabled: boolean };

export type Profile = {
  id: string;
  userId: string;
  displayName: string;
  email: string | null;
  bio: string | null;
  homeRegion: string | null;
  avatarUrl: string | null;
  preferences: Preferences;
  totals: { completedHikes: number; distanceMeters: number; elevationGainMeters: number; longestHikeMeters: number; biggestClimbMeters: number };
  achievementCount: number;
  createdAtUtc: string;
};

export type Achievement = { code: string; title: string; description: string; unlocked: boolean; unlockedAtUtc: string | null };

export type HikingSuitability = 'Good' | 'Caution' | 'Poor';
export type WeatherCondition =
  | 'Clear' | 'PartlyCloudy' | 'Overcast' | 'Fog' | 'Drizzle' | 'Rain' | 'FreezingRain' | 'Snow' | 'Showers' | 'Thunderstorm' | 'Unknown';

export type DailyForecast = {
  forecast: {
    date: string;
    minTemperatureC: number;
    maxTemperatureC: number;
    precipitationMm: number;
    precipitationProbabilityPercent: number;
    maxWindGustsKmh: number;
    sunriseLocal: string;
    sunsetLocal: string;
  };
  condition: WeatherCondition;
  assessment: { suitability: HikingSuitability; warnings: string[] };
};

export type Forecast = {
  latitude: number;
  longitude: number;
  timeZone: string;
  current: { temperatureC: number; apparentTemperatureC: number; windSpeedKmh: number; windGustsKmh: number };
  currentCondition: WeatherCondition;
  daily: DailyForecast[];
};

export type TripStatus = 'Planned' | 'Completed' | 'Cancelled';
export type TripSummary = {
  id: string;
  name: string;
  startDate: string;
  endDate: string;
  dayCount: number;
  status: TripStatus;
  trailCount: number;
  groupId: string | null;
  groupName: string | null;
  isOwner: boolean;
  /** Group gear nobody has claimed yet. */
  sharedUnclaimed: number;
};
export type ChecklistItemKind = 'Personal' | 'Shared';
export type ChecklistItem = {
  id: string;
  text: string;
  kind: ChecklistItemKind;
  /** Personal: whether I've packed mine. Shared: whether the person bringing it has packed it. */
  isDone: boolean;
  claimedBy: string | null;
  claimedByName: string | null;
  addedBy: string;
  canRemove: boolean;
};
export type TripMember = { userId: string; displayName: string; isOwner: boolean; personalDone: number };
export type Trip = {
  id: string;
  name: string;
  description: string | null;
  startDate: string;
  endDate: string;
  dayCount: number;
  status: TripStatus;
  days: { dayNumber: number; date: string; notes: string | null; overnight: string | null; trailIds: string[] }[];
  checklist: ChecklistItem[];
  ownerId: string;
  isOwner: boolean;
  groupId: string | null;
  groupName: string | null;
  /** The group's members and how many personal items each has packed; empty for private trips. */
  members: TripMember[];
  /** A planned route for the whole trip; its day N is the trip's day N. */
  routeId: string | null;
};

export type GroupRole = 'Owner' | 'Member';
export type GroupSummary = { id: string; name: string; memberCount: number; myRole: GroupRole; nextHikeAtUtc: string | null };
export type RsvpStatus = 'Going' | 'Maybe' | 'NotGoing';
export type GroupHike = {
  id: string;
  title: string;
  trailId: string | null;
  scheduledAtUtc: string;
  meetingLatitude: number | null;
  meetingLongitude: number | null;
  notes: string | null;
  createdBy: string;
  status: 'Scheduled' | 'Cancelled';
  rsvps: { userId: string; status: RsvpStatus; respondedAtUtc: string }[];
};
export type Group = {
  id: string;
  name: string;
  description: string | null;
  inviteCode: string | null;
  members: { userId: string; displayName: string; role: GroupRole; joinedAtUtc: string }[];
  upcomingHikes: GroupHike[];
};
export type LiveLocation = {
  userId: string;
  latitude: number;
  longitude: number;
  elevation: number | null;
  accuracyMeters: number | null;
  headingDegrees: number | null;
  recordedAtUtc: string;
};

// Route planner -------------------------------------------------------------------------

export type PoiKind = 'WaterSource' | 'Campsite' | 'MountainHut' | 'Shelter' | 'Hotel' | 'Viewpoint';
export type Accommodation = 'Tent' | 'Hut' | 'Hotel';
export type PlanDifficulty = 'Easy' | 'Moderate' | 'Hard' | 'Expert';
export type RouteWaypoint = { latitude: number; longitude: number; name?: string | null; isOvernight: boolean; kind?: PoiKind | null };
export type TrackStats = {
  distanceMeters: number;
  ascentMeters: number;
  descentMeters: number;
  minElevationMeters: number | null;
  maxElevationMeters: number | null;
  durationMinutes: number;
  /** Hardest SAC grade: 1 = T1 … 6 = T6; 0 = unknown. */
  maxSacScale: number;
};
export type RouteDay = { day: number; startDistanceMeters: number; stats: TrackStats; overnight: RouteWaypoint | null };
export type PlannedRoute = {
  id: string | null;
  name: string | null;
  description: string | null;
  waypoints: RouteWaypoint[];
  /** [latitude, longitude, elevation] */
  path: [number, number, number | null][];
  stats: TrackStats;
  elevationProfile: ElevationSample[];
  days: RouteDay[];
  /** False when the line is straight between waypoints (no paths found or routing not configured). */
  snapped: boolean;
  warnings: string[];
  updatedAtUtc: string | null;
  /** False when someone shared it with your group: you can follow and copy it, not change it. */
  isOwner: boolean;
  sharedWith: { id: string; name: string }[] | null;
  /** Set once published to Discover (as a trail with the same id). */
  publishedAtUtc: string | null;
};
export type RouteSummary = {
  id: string;
  name: string;
  distanceMeters: number;
  ascentMeters: number;
  durationMinutes: number;
  dayCount: number;
  snapped: boolean;
  startLatitude: number;
  startLongitude: number;
  updatedAtUtc: string;
  ownerId: string;
  isOwner: boolean;
  published: boolean;
};
export type Poi = { id: string; name: string; kind: PoiKind; latitude: number; longitude: number; elevation: number | null };
export type Place = { name: string; region: string | null; latitude: number; longitude: number };
