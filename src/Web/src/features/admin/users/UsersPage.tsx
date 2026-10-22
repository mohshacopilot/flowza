import { PlusOutlined } from '@ant-design/icons';
import { keepPreviousData } from '@tanstack/react-query';
import { Button, Input, Select, Table, Tag, Typography, type TableColumnsType } from 'antd';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useNavigate, useSearchParams } from 'react-router';
import { describeApiError } from '../../../shared/api/errorMessages';
import { UserStatus, type UserListItem } from '../../../shared/api/generated/identity/model';
import { useListUsers } from '../../../shared/api/generated/identity/users/users';
import { ApiErrorAlert } from '../../../shared/components/ApiErrorAlert';
import { roleLabel } from './roleLabel';
import { UserStatusTag } from './UserStatusTag';
import styles from './users.module.css';

/** Page size of every list (CLAUDE.md conventions: page + size 25 + total). */
const pageSize = 25;

const statuses = Object.values(UserStatus);

function readPage(value: string | null): number {
  const page = Number(value);
  return Number.isInteger(page) && page > 0 ? page : 1;
}

function readStatus(value: string | null): UserStatus | undefined {
  return statuses.find((status) => status === value);
}

/**
 * The users list for Platform Admins (IAM-04): search, status filter and paging. The filter lives in the
 * address (?search=&status=&page=), so "back" from a user returns to the same page of the list.
 */
export function UsersPage() {
  const { t, i18n } = useTranslation();
  const navigate = useNavigate();
  const [params, setParams] = useSearchParams();
  const page = readPage(params.get('page'));
  const search = params.get('search') ?? '';
  const status = readStatus(params.get('status'));
  const [searchText, setSearchText] = useState(search);

  const users = useListUsers(
    { page, size: pageSize, ...(search ? { search } : {}), ...(status ? { status } : {}) },
    { query: { placeholderData: keepPreviousData } },
  );

  /** Changes the filter in the address; a new search or status starts again at page 1. */
  const update = (changes: { search?: string; status?: UserStatus | undefined; page?: number }) => {
    const next = new URLSearchParams(params);
    const set = (key: string, value: string | undefined) => {
      if (value) {
        next.set(key, value);
      } else {
        next.delete(key);
      }
    };
    if ('search' in changes) {
      set('search', changes.search.trim());
    }
    if ('status' in changes) {
      set('status', changes.status);
    }
    set('page', changes.page && changes.page > 1 ? String(changes.page) : undefined);
    setParams(next);
  };

  const columns: TableColumnsType<UserListItem> = [
    {
      key: 'username',
      title: t('users.columns.username'),
      render: (_, user) => <Link to={`/admin/users/${user.id}`}>{user.username}</Link>,
    },
    { key: 'displayName', title: t('users.columns.displayName'), dataIndex: 'displayName' },
    { key: 'email', title: t('users.columns.email'), dataIndex: 'email' },
    {
      key: 'roles',
      title: t('users.columns.roles'),
      render: (_, user) =>
        user.roles.map((role) => (
          <Tag key={role} variant="filled">
            {roleLabel(t, role)}
          </Tag>
        )),
    },
    {
      key: 'status',
      title: t('users.columns.status'),
      render: (_, user) => <UserStatusTag status={user.status} />,
    },
  ];

  return (
    <>
      <Typography.Title level={1}>{t('users.title')}</Typography.Title>
      <Typography.Paragraph>{t('users.intro')}</Typography.Paragraph>

      <div className={styles.toolbar}>
        <Input.Search
          className={styles.search}
          aria-label={t('users.search')}
          placeholder={t('users.search')}
          value={searchText}
          onChange={(event) => {
            setSearchText(event.target.value);
          }}
          onSearch={(value) => {
            update({ search: value });
          }}
          allowClear
        />
        <Select<UserStatus | 'all'>
          style={{ minWidth: 180 }}
          aria-label={t('users.statusFilter')}
          value={status ?? 'all'}
          onChange={(value) => {
            update({ status: value === 'all' ? undefined : value });
          }}
          options={[
            { value: 'all', label: t('users.allStatuses') },
            ...statuses.map((s) => ({ value: s, label: t(`users.status.${s}`) })),
          ]}
        />
        <div className={styles.toolbarEnd}>
          <Button
            type="primary"
            icon={<PlusOutlined aria-hidden />}
            onClick={() => {
              void navigate('/admin/users/new');
            }}
          >
            {t('users.newUser')}
          </Button>
        </div>
      </div>

      {users.isError && <ApiErrorAlert error={describeApiError(users.error, i18n)} />}

      <Table<UserListItem>
        aria-label={t('users.listLabel')}
        rowKey="id"
        // On a narrow screen the table scrolls inside itself instead of widening the whole page.
        scroll={{ x: 'max-content' }}
        columns={columns}
        dataSource={users.data?.items ?? []}
        loading={users.isFetching}
        locale={{ emptyText: t('users.noUsers') }}
        pagination={{
          current: page,
          pageSize,
          total: Number(users.data?.totalCount ?? 0),
          showSizeChanger: false,
          hideOnSinglePage: false,
          showTotal: (total, [start, end]) => t('users.pageSummary', { start, end, total }),
          onChange: (next) => {
            update({ page: next });
          },
        }}
      />
    </>
  );
}
